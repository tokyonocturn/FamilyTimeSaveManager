using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.Core.Storage;

public sealed class SettingsStore
{
    private readonly object _gate = new();
    private readonly string _path;
    private AppSettings _settings;

    public SettingsStore(AppPaths paths)
    {
        _path = paths.SettingsFile;
        _settings = JsonFile.Load(_path, () => new AppSettings());
        Normalize(_settings);
    }

    public event Action<AppSettings>? Changed;

    public AppSettings Current
    {
        get
        {
            lock (_gate)
            {
                return _settings.Clone();
            }
        }
    }

    public void Update(Action<AppSettings> change)
    {
        AppSettings snapshot;
        lock (_gate)
        {
            var working = _settings.Clone();
            change(working);
            Normalize(working);
            JsonFile.Save(_path, working);
            _settings = working;
            snapshot = working.Clone();
        }

        Changed?.Invoke(snapshot);
    }

    public void Replace(AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        Update(current =>
        {
            var replacement = settings.Clone();
            current.ExtraInstallPaths = replacement.ExtraInstallPaths;
            current.InstallEpisodeOverrides = replacement.InstallEpisodeOverrides;
            current.InstallGraphicsOverrides = replacement.InstallGraphicsOverrides;
            current.Episode1SavePathOverride = replacement.Episode1SavePathOverride;
            current.Episode2SavePathOverride = replacement.Episode2SavePathOverride;
            current.Episode3SavePathOverride = replacement.Episode3SavePathOverride;
            current.AutoBackupEnabled = replacement.AutoBackupEnabled;
            current.AutoBackupIntervalMinutes = replacement.AutoBackupIntervalMinutes;
            current.RetentionAutoBackups = replacement.RetentionAutoBackups;
            current.RetentionDays = replacement.RetentionDays;
            current.AutoBackupOnSaveChange = replacement.AutoBackupOnSaveChange;
            current.WarnWhenGameRunning = replacement.WarnWhenGameRunning;
            current.VerifyChecksumsOnRestore = replacement.VerifyChecksumsOnRestore;
            current.LastSelectedEpisode = replacement.LastSelectedEpisode;
            current.AccentColor = replacement.AccentColor;
        });
    }

    private static void Normalize(AppSettings settings)
    {
        settings.ExtraInstallPaths = settings.ExtraInstallPaths
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        settings.AutoBackupIntervalMinutes = Math.Clamp(settings.AutoBackupIntervalMinutes, 5, 24 * 60);
        settings.RetentionAutoBackups = Math.Clamp(settings.RetentionAutoBackups, 1, 500);
        settings.RetentionDays = Math.Clamp(settings.RetentionDays, 0, 3650);
    }
}
