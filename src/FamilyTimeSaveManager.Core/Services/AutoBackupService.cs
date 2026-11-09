using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Services;

/// <summary>
/// Scheduled backups while the application is open. The service remembers the SHA-256 of each save
/// it has seen and only creates a new backup when the file changes, unless the user disabled
/// change detection. Retention then prunes old scheduled backups only.
/// </summary>
public sealed class AutoBackupService : IDisposable
{
    private readonly Func<CancellationToken, IReadOnlyList<SaveFileInfo>> _saveProvider;
    private readonly BackupService _backups;
    private readonly BackupRepository _repository;
    private readonly SettingsStore _settings;
    private readonly RetentionService _retention;
    private readonly HistoryStore _history;
    private readonly ActivityLog _log;
    private readonly Dictionary<string, string> _lastSeen = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    private Timer? _timer;
    private int _running;
    private bool _disposed;

    public AutoBackupService(
        Func<CancellationToken, IReadOnlyList<SaveFileInfo>> saveProvider,
        BackupService backups,
        BackupRepository repository,
        SettingsStore settings,
        RetentionService retention,
        HistoryStore history,
        ActivityLog log)
    {
        _saveProvider = saveProvider;
        _backups = backups;
        _repository = repository;
        _settings = settings;
        _retention = retention;
        _history = history;
        _log = log;
    }

    public event Action<string>? StatusChanged;

    public event Action<BackupRecord>? BackupCreated;

    public bool IsRunning => _timer is not null;

    public void Start()
    {
        Stop();
        var settings = _settings.Current;
        if (!settings.AutoBackupEnabled)
        {
            StatusChanged?.Invoke("Scheduled backups are off.");
            return;
        }

        var interval = TimeSpan.FromMinutes(settings.AutoBackupIntervalMinutes);
        _timer = new Timer(_ => RunSafely(manual: false), null, TimeSpan.FromMinutes(1), interval);
        StatusChanged?.Invoke($"Scheduled backups every {settings.AutoBackupIntervalMinutes} minute(s).");
        _log.Info("schedule", $"Scheduled backups armed for every {settings.AutoBackupIntervalMinutes} minute(s).");
    }

    public void Stop()
    {
        _timer?.Dispose();
        _timer = null;
    }

    /// <summary>
    /// Runs one scheduling pass. Exposed so tests and the "Run now" button do not wait for a timer.
    /// </summary>
    public int RunOnce(bool manual = false, CancellationToken cancellationToken = default)
    {
        var settings = _settings.Current;
        if (!manual && !settings.AutoBackupEnabled)
        {
            return 0;
        }

        if (Interlocked.Exchange(ref _running, 1) == 1)
        {
            return 0;
        }

        try
        {
            IReadOnlyList<SaveFileInfo> saves;
            try
            {
                saves = _saveProvider(cancellationToken);
            }
            catch (Exception ex)
            {
                _log.Warn("schedule", "Could not read saves for a scheduled backup: " + ex.Message);
                StatusChanged?.Invoke("Scheduled backup skipped: save folder could not be read.");
                return 0;
            }

            var created = 0;
            foreach (var save in saves)
            {
                cancellationToken.ThrowIfCancellationRequested();

                if (!File.Exists(save.Path))
                {
                    continue;
                }

                string? hash = save.Sha256;
                if (string.IsNullOrWhiteSpace(hash))
                {
                    try
                    {
                        hash = FileOps.ComputeSha256(save.Path);
                    }
                    catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                    {
                        _log.Warn("schedule", $"Skipping '{save.FileName}': {ex.Message}");
                        continue;
                    }
                }

                bool changed;
                lock (_gate)
                {
                    changed = !_lastSeen.TryGetValue(save.Path, out var previous)
                              || !string.Equals(previous, hash, StringComparison.OrdinalIgnoreCase);
                }

                if (!changed && settings.AutoBackupOnSaveChange)
                {
                    continue;
                }

                try
                {
                    save.Sha256 = hash;
                    var record = _backups.Create(
                        save,
                        $"Scheduled: {save.BaseName}",
                        BuildDescription(save),
                        Profile.DefaultId,
                        BackupKind.Automatic);

                    lock (_gate)
                    {
                        _lastSeen[save.Path] = hash!;
                    }

                    created++;
                    BackupCreated?.Invoke(record);
                }
                catch (Exception ex)
                {
                    _log.Warn("schedule", $"Scheduled backup of '{save.FileName}' failed: {ex.Message}");
                    StatusChanged?.Invoke($"Scheduled backup failed for {save.FileName}.");
                }
            }

            Prune(settings);

            StatusChanged?.Invoke(created == 0
                ? "Scheduled backup check finished: nothing changed."
                : $"Scheduled backup finished: {created} backup(s) created.");
            return created;
        }
        finally
        {
            Interlocked.Exchange(ref _running, 0);
        }
    }

    public void ResetChangeTracking()
    {
        lock (_gate)
        {
            _lastSeen.Clear();
        }
    }

    private void Prune(AppSettings settings)
    {
        var expired = _retention.SelectExpired(_repository.List(), settings);
        if (expired.Count == 0)
        {
            return;
        }

        foreach (var record in expired)
        {
            try
            {
                _repository.MoveToTrash(record.Id);
                _history.Add(new HistoryEntry
                {
                    Action = HistoryAction.Schedule,
                    Status = HistoryStatus.Success,
                    Summary = $"Retention removed scheduled backup '{record.Name}'",
                    Detail = $"Kept at most {settings.RetentionAutoBackups} scheduled backup(s) per profile" +
                             (settings.RetentionDays > 0 ? $" and nothing older than {settings.RetentionDays} day(s)" : string.Empty) + ".",
                    BackupId = record.Id,
                    Bytes = record.TotalBytes
                });
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn("schedule", $"Retention could not remove '{record.Name}': {ex.Message}");
            }
        }
    }

    private void RunSafely(bool manual)
    {
        try
        {
            RunOnce(manual);
        }
        catch (Exception ex)
        {
            _log.Error("schedule", "Scheduled backup pass failed: " + ex.Message);
        }
    }

    private static string? BuildDescription(SaveFileInfo save)
    {
        var parts = new List<string>
        {
            $"{save.Episode.ToDisplayName()}",
            save.SizeDisplay,
            $"modified {save.ModifiedUtc.ToLocalTime():g}"
        };

        if (!string.IsNullOrWhiteSpace(save.StatusMessage))
        {
            parts.Add(save.StatusMessage!);
        }

        return string.Join(", ", parts);
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        Stop();
    }
}
