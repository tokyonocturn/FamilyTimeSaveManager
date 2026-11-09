namespace FamilyTimeSaveManager.Core.Models;

public sealed class AppSettings
{
    public List<string> ExtraInstallPaths { get; set; } = new();

    /// <summary>Manual install path to episode assignments, keyed by full install path.</summary>
    public Dictionary<string, string> InstallEpisodeOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Manual install path to renderer assignments, keyed by full install path.</summary>
    public Dictionary<string, string> InstallGraphicsOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    public string? Episode1SavePathOverride { get; set; }

    public string? Episode2SavePathOverride { get; set; }

    public string? Episode3SavePathOverride { get; set; }

    public bool AutoBackupEnabled { get; set; } = true;

    public int AutoBackupIntervalMinutes { get; set; } = 30;

    /// <summary>How many scheduled backups to keep per profile. Manual and pre-restore backups are never pruned automatically.</summary>
    public int RetentionAutoBackups { get; set; } = 20;

    /// <summary>Delete scheduled backups older than this many days. Zero disables age-based pruning.</summary>
    public int RetentionDays { get; set; } = 0;

    public bool AutoBackupOnSaveChange { get; set; } = true;

    public bool WarnWhenGameRunning { get; set; } = true;

    public bool VerifyChecksumsOnRestore { get; set; } = true;

    public string? LastSelectedEpisode { get; set; }

    public string AccentColor { get; set; } = "#5B8DFF";

    public string? GetSavePathOverride(Episode episode) => episode switch
    {
        Episode.Episode1 => Episode1SavePathOverride,
        Episode.Episode2 => Episode2SavePathOverride,
        Episode.Episode3 => Episode3SavePathOverride,
        _ => null
    };

    public void SetSavePathOverride(Episode episode, string? path)
    {
        switch (episode)
        {
            case Episode.Episode1:
                Episode1SavePathOverride = path;
                break;
            case Episode.Episode2:
                Episode2SavePathOverride = path;
                break;
            case Episode.Episode3:
                Episode3SavePathOverride = path;
                break;
        }
    }

    public AppSettings Clone()
    {
        return new AppSettings
        {
            ExtraInstallPaths = new List<string>(ExtraInstallPaths),
            InstallEpisodeOverrides = new Dictionary<string, string>(InstallEpisodeOverrides, StringComparer.OrdinalIgnoreCase),
            InstallGraphicsOverrides = new Dictionary<string, string>(InstallGraphicsOverrides, StringComparer.OrdinalIgnoreCase),
            Episode1SavePathOverride = Episode1SavePathOverride,
            Episode2SavePathOverride = Episode2SavePathOverride,
            Episode3SavePathOverride = Episode3SavePathOverride,
            AutoBackupEnabled = AutoBackupEnabled,
            AutoBackupIntervalMinutes = AutoBackupIntervalMinutes,
            RetentionAutoBackups = RetentionAutoBackups,
            RetentionDays = RetentionDays,
            AutoBackupOnSaveChange = AutoBackupOnSaveChange,
            WarnWhenGameRunning = WarnWhenGameRunning,
            VerifyChecksumsOnRestore = VerifyChecksumsOnRestore,
            LastSelectedEpisode = LastSelectedEpisode,
            AccentColor = AccentColor
        };
    }
}
