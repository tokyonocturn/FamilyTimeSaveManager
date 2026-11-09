using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Services;

public sealed record RestoreOptions
{
    /// <summary>Always create a pre-restore backup of the live files (the default).</summary>
    public bool CreatePreRestoreBackup { get; init; } = true;

    /// <summary>Overrides the destination folder for every file in the backup.</summary>
    public string? DestinationFolder { get; init; }

    public string? ProfileId { get; init; }
}

public sealed record RestoreResult(
    bool Success,
    string Message,
    string? PreRestoreBackupId,
    IReadOnlyList<string> WrittenFiles,
    IReadOnlyList<string> Errors);

/// <summary>
/// Restores a verified backup after first snapshotting the live files. Restores are per-file
/// atomic replacements; the operation refuses to continue if a destination is unsafe or if the
/// backup no longer matches its manifest.
/// </summary>
public sealed class RestoreService
{
    private readonly AppPaths _paths;
    private readonly BackupRepository _repository;
    private readonly BackupService _backups;
    private readonly HistoryStore _history;
    private readonly ActivityLog _log;

    public RestoreService(
        AppPaths paths,
        BackupRepository repository,
        BackupService backups,
        HistoryStore history,
        ActivityLog log)
    {
        _paths = paths;
        _repository = repository;
        _backups = backups;
        _history = history;
        _log = log;
    }

    public RestoreResult Restore(BackupRecord record, RestoreOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(record);
        options ??= new RestoreOptions();

        var verification = _backups.Verify(record);
        if (!verification.Ok)
        {
            var message = "The backup failed verification, so nothing was restored. " + string.Join(" ", verification.Errors);
            Record(record, HistoryStatus.Failed, "Restore blocked: backup integrity check failed", message);
            return new RestoreResult(false, message, null, Array.Empty<string>(), verification.Errors.ToList());
        }

        if (record.Files.Count == 0)
        {
            var message = "The backup contains no files.";
            Record(record, HistoryStatus.Failed, "Restore blocked: empty backup", message);
            return new RestoreResult(false, message, null, Array.Empty<string>(), new[] { message });
        }

        var targets = new List<(BackupFileEntry Entry, string Destination)>();
        foreach (var entry in record.Files)
        {
            var destination = ResolveDestination(entry, options);
            if (destination is null)
            {
                var message = $"The destination for '{entry.OriginalFileName}' is missing or unsafe, so nothing was restored.";
                Record(record, HistoryStatus.Failed, "Restore blocked: unsafe destination", message);
                return new RestoreResult(false, message, null, Array.Empty<string>(), new[] { message });
            }

            targets.Add((entry, destination));
        }

        var destinationFolders = targets
            .Select(t => Path.GetDirectoryName(t.Destination)!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        foreach (var folder in destinationFolders)
        {
            if (!Directory.Exists(folder))
            {
                var message = $"The destination folder '{folder}' does not exist. Create it in Settings or start the game once, then try again.";
                Record(record, HistoryStatus.Failed, "Restore blocked: missing save folder", message);
                return new RestoreResult(false, message, null, Array.Empty<string>(), new[] { message });
            }
        }

        string? preRestoreId = null;
        if (options.CreatePreRestoreBackup)
        {
            var live = BuildLiveSnapshot(record, targets);
            if (live is not null)
            {
                try
                {
                    var preRestore = _backups.Create(
                        live,
                        $"Before restoring '{record.Name}'",
                        $"Automatic snapshot taken {DateTime.Now:g} before restoring backup {record.Id}.",
                        options.ProfileId ?? record.ProfileId,
                        BackupKind.PreRestore,
                        record.GraphicsApiLabel,
                        record.BuildLabel);
                    preRestoreId = preRestore.Id;
                }
                catch (Exception ex)
                {
                    var message = "The pre-restore backup could not be created, so the live saves were left untouched. " + ex.Message;
                    Record(record, HistoryStatus.Failed, "Restore blocked: pre-restore backup failed", message);
                    return new RestoreResult(false, message, null, Array.Empty<string>(), new[] { message });
                }
            }
        }

        var written = new List<string>();
        var errors = new List<string>();
        var filesFolder = _paths.BackupFilesFolder(record.Id);

        foreach (var (entry, destination) in targets)
        {
            var source = Path.Combine(filesFolder, FileOps.SanitizeFileName(entry.StoredName, "file"));
            try
            {
                if (!FileOps.IsWithin(filesFolder, source) || !File.Exists(source))
                {
                    errors.Add($"The backup copy of '{entry.OriginalFileName}' is missing.");
                    continue;
                }

                var sourceHash = FileOps.ComputeSha256(source);
                if (!string.Equals(sourceHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"The backup copy of '{entry.OriginalFileName}' changed after verification.");
                    continue;
                }

                FileOps.CopyFileAtomic(source, destination);

                var restoredHash = FileOps.ComputeSha256(destination);
                if (!string.Equals(restoredHash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    errors.Add($"'{destination}' did not match the backup checksum after it was written.");
                    continue;
                }

                written.Add(destination);

                if (entry.Role == "primary")
                {
                    var inFolder = Path.Combine(Path.GetDirectoryName(destination)!, ".restore-info.txt");
                    TryWriteRestoreNote(inFolder, record);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"Could not restore '{destination}': {ex.Message}");
            }
        }

        if (errors.Count == 0)
        {
            Record(
                record,
                HistoryStatus.Success,
                $"Restored '{record.Name}'",
                $"Wrote {written.Count} file(s). Pre-restore backup: {preRestoreId ?? "not needed"}. " + string.Join(" ", written));
            _log.Info("restore", $"Restored backup '{record.Name}' ({record.KindDisplay}) to {string.Join(", ", written)}.");
            return new RestoreResult(true, $"Restored {written.Count} file(s) and verified every checksum.", preRestoreId, written, Array.Empty<string>());
        }

        Record(
            record,
            HistoryStatus.Warning,
            $"Restore of '{record.Name}' finished with errors",
            $"{written.Count} file(s) written, {errors.Count} error(s). Pre-restore backup: {preRestoreId ?? "none"}. " + string.Join(" ", errors));
        _log.Warn("restore", $"Restore of {record.Name} finished with {errors.Count} error(s).");
        return new RestoreResult(
            false,
            "The restore finished with errors. A pre-restore backup was kept so the original state can be recovered.",
            preRestoreId,
            written,
            errors);
    }

    private SaveFileInfo? BuildLiveSnapshot(BackupRecord record, IReadOnlyList<(BackupFileEntry Entry, string Destination)> targets)
    {
        var primary = targets.FirstOrDefault(t => t.Entry.Role == "primary");
        if (primary.Entry is null || !File.Exists(primary.Destination))
        {
            return null;
        }

        var info = new FileInfo(primary.Destination);
        var save = new SaveFileInfo
        {
            Path = primary.Destination,
            Episode = record.Episode,
            Folder = Path.GetDirectoryName(primary.Destination),
            Length = info.Length,
            CreatedUtc = info.CreationTimeUtc,
            ModifiedUtc = info.LastWriteTimeUtc,
            Sha256 = FileOps.ComputeSha256(primary.Destination),
            Status = SaveStatus.Unknown,
            Metadata = new SaveMetadata
            {
                Format = record.Episode == Episode.Episode3 ? "Episode 3 .family" : "Legacy .world"
            }
        };

        foreach (var (entry, destination) in targets.Where(t => t.Entry.Role == "sidecar"))
        {
            if (File.Exists(destination))
            {
                save.SidecarPaths.Add(destination);
            }
        }

        return save;
    }

    private static string? ResolveDestination(BackupFileEntry entry, RestoreOptions options)
    {
        var fileName = FileOps.SanitizeFileName(entry.OriginalFileName, "save");
        if (!string.Equals(fileName, entry.OriginalFileName, StringComparison.Ordinal))
        {
            return null;
        }

        string? folder;
        if (!string.IsNullOrWhiteSpace(options.DestinationFolder))
        {
            folder = Path.GetFullPath(options.DestinationFolder!);
        }
        else
        {
            folder = Path.GetDirectoryName(entry.OriginalPath);
        }

        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var destination = Path.GetFullPath(Path.Combine(folder, fileName));
        if (!IsSafeDestination(destination))
        {
            return null;
        }

        return destination;
    }

    private static bool IsSafeDestination(string destination)
    {
        var root = Path.GetPathRoot(destination);
        if (string.IsNullOrEmpty(root) || string.Equals(root, destination, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var windows = Environment.GetFolderPath(Environment.SpecialFolder.Windows);
        if (!string.IsNullOrEmpty(windows) && FileOps.IsWithin(windows, destination))
        {
            return false;
        }

        return true;
    }

    private static void TryWriteRestoreNote(string folder, BackupRecord record)
    {
        try
        {
            var path = Path.Combine(folder, ".restore-info.txt");
            var text = $"FamilyTime Save Manager restored a backup on {DateTime.Now:g}.{Environment.NewLine}" +
                       $"Backup: {record.Name} ({record.Id}){Environment.NewLine}" +
                       $"Original source: {record.SourcePath}{Environment.NewLine}";
            FileOps.WriteAllTextAtomic(path, text);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
        }
    }

    private void Record(BackupRecord record, HistoryStatus status, string summary, string detail)
    {
        _history.Add(new HistoryEntry
        {
            Action = HistoryAction.Restore,
            Status = status,
            Summary = summary,
            Detail = detail,
            BackupId = record.Id,
            SavePath = record.SourcePath,
            Sha256 = record.PrimaryFile?.Sha256,
            Bytes = record.TotalBytes
        });
    }
}
