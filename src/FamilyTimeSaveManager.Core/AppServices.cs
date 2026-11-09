using System.Diagnostics;
using FamilyTimeSaveManager.Core.Detection;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Services;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core;

/// <summary>
/// Composition root. One instance owns every service and the local data folder; nothing leaves
/// the machine.
/// </summary>
public sealed class AppServices : IDisposable
{
    private AppServices(string dataRoot)
    {
        Paths = new AppPaths(dataRoot);
        Log = new ActivityLog(Paths);
        Settings = new SettingsStore(Paths);
        Profiles = new ProfileStore(Paths);
        History = new HistoryStore(Paths);
        Backups = new BackupRepository(Paths, Log);
        Detector = new GameInstallDetector(Log, Settings);
        Locations = new SaveLocationService(Settings);
        Scanner = new SaveScanner(Log);
        BackupService = new BackupService(Paths, Backups, History, Log);
        RestoreService = new RestoreService(Paths, Backups, BackupService, History, Log);
        ArchiveService = new ArchiveService(Paths, Backups, BackupService, History, Log);
        Retention = new RetentionService();
        AutoBackup = new AutoBackupService(
            ct => ScanSaves(verifyChecksums: false, ct),
            BackupService,
            Backups,
            Settings,
            Retention,
            History,
            Log);

        Log.Info("app", $"Data folder: {Paths.DataRoot}");
    }

    public AppPaths Paths { get; }
    public ActivityLog Log { get; }
    public SettingsStore Settings { get; }
    public ProfileStore Profiles { get; }
    public HistoryStore History { get; }
    public BackupRepository Backups { get; }
    public GameInstallDetector Detector { get; }
    public SaveLocationService Locations { get; }
    public SaveScanner Scanner { get; }
    public BackupService BackupService { get; }
    public RestoreService RestoreService { get; }
    public ArchiveService ArchiveService { get; }
    public RetentionService Retention { get; }
    public AutoBackupService AutoBackup { get; }

    public static AppServices Create(string? dataRoot = null) =>
        new(string.IsNullOrWhiteSpace(dataRoot) ? AppPaths.CreateDefault().DataRoot : dataRoot!);

    public IReadOnlyList<GameInstall> DetectInstalls(CancellationToken cancellationToken = default) =>
        Detector.Detect(cancellationToken: cancellationToken);

    public IReadOnlyList<SaveLocation> ResolveLocations(IReadOnlyList<GameInstall>? installs = null) =>
        Locations.Resolve(installs ?? DetectInstalls());

    public IReadOnlyList<SaveFileInfo> ScanSaves(bool verifyChecksums = true, CancellationToken cancellationToken = default) =>
        Scanner.Scan(ResolveLocations(), verifyChecksums, cancellationToken);

    public static bool IsGameRunning()
    {
        try
        {
            var processes = Process.GetProcessesByName("FamilyTime");
            try
            {
                return processes.Length > 0;
            }
            finally
            {
                foreach (var process in processes)
                {
                    process.Dispose();
                }
            }
        }
        catch (Exception ex) when (ex is InvalidOperationException or System.ComponentModel.Win32Exception)
        {
            return false;
        }
    }

    public void Dispose()
    {
        AutoBackup.Dispose();
        Log.Dispose();
    }
}
