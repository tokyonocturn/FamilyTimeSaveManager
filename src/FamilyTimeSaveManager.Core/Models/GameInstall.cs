namespace FamilyTimeSaveManager.Core.Models;

/// <summary>
/// A Family Time installation found on disk. Detection never writes to the install.
/// </summary>
public sealed class GameInstall
{
    public string InstallPath { get; set; } = string.Empty;
    public Episode Episode { get; set; } = Episode.Unknown;
    public GraphicsApi GraphicsApi { get; set; } = GraphicsApi.Unknown;
    public string? CompanyName { get; set; }
    public string? ProductName { get; set; }
    public string? UnityVersion { get; set; }
    public string? BuildLabel { get; set; }
    public string DetectionSource { get; set; } = "scan";
    public bool EpisodeIsGuessed { get; set; }
    public bool GraphicsApiIsGuessed { get; set; }
    public List<string> Evidence { get; } = new();

    public string ExePath => Path.Combine(InstallPath, "FamilyTime.exe");

    public string DataPath => Path.Combine(InstallPath, "FamilyTime_Data");

    public string? ManagedPath
    {
        get
        {
            var path = Path.Combine(DataPath, "Managed");
            return Directory.Exists(path) ? path : null;
        }
    }

    /// <summary>
    /// Episode 3 stores sessions inside the install folder. Episodes 1 and 2 use the
    /// Unity persistent data folder (LocalLow), which both builds share.
    /// </summary>
    public bool SavesInsideInstall => Episode == Episode.Episode3;

    public string? SessionsFolder => SavesInsideInstall ? Path.Combine(DataPath, "Sessions") : null;

    public string DisplayName
    {
        get
        {
            var episode = Episode == Episode.Unknown ? "Unidentified episode" : Episode.ToDisplayName();
            var api = GraphicsApi == GraphicsApi.Unknown ? string.Empty : $" ({GraphicsApi.ToShortLabel()})";
            var build = string.IsNullOrWhiteSpace(BuildLabel) ? string.Empty : $" build {BuildLabel}";
            return $"{episode}{api}{build}";
        }
    }

    public string FolderName
    {
        get
        {
            var trimmed = InstallPath.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return Path.GetFileName(trimmed) is { Length: > 0 } name ? name : trimmed;
        }
    }
}
