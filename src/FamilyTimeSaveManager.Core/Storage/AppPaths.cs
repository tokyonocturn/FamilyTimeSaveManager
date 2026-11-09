namespace FamilyTimeSaveManager.Core.Storage;

/// <summary>
/// Every file the application owns lives under one local folder. Nothing is written anywhere else,
/// and nothing is ever uploaded.
/// </summary>
public sealed class AppPaths
{
    public const string ApplicationFolderName = "FamilyTimeSaveManager";

    public AppPaths(string dataRoot)
    {
        if (string.IsNullOrWhiteSpace(dataRoot))
        {
            throw new ArgumentException("A data root is required.", nameof(dataRoot));
        }

        DataRoot = Path.GetFullPath(dataRoot);
        BackupsRoot = Path.Combine(DataRoot, "backups");
        LogsRoot = Path.Combine(DataRoot, "logs");
        ExportsRoot = Path.Combine(DataRoot, "exports");
        SettingsFile = Path.Combine(DataRoot, "settings.json");
        ProfilesFile = Path.Combine(DataRoot, "profiles.json");
        HistoryFile = Path.Combine(DataRoot, "history.json");

        Directory.CreateDirectory(DataRoot);
        Directory.CreateDirectory(BackupsRoot);
        Directory.CreateDirectory(LogsRoot);
        Directory.CreateDirectory(ExportsRoot);
    }

    public string DataRoot { get; }
    public string BackupsRoot { get; }
    public string LogsRoot { get; }
    public string ExportsRoot { get; }
    public string SettingsFile { get; }
    public string ProfilesFile { get; }
    public string HistoryFile { get; }

    public static AppPaths CreateDefault() => new(
        Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            ApplicationFolderName));

    public string BackupFolder(string id) => Path.Combine(BackupsRoot, id);

    public string BackupFilesFolder(string id) => Path.Combine(BackupFolder(id), "files");

    public string BackupManifestFile(string id) => Path.Combine(BackupFolder(id), "manifest.json");
}
