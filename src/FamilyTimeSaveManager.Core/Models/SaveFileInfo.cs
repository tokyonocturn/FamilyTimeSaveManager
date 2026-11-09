namespace FamilyTimeSaveManager.Core.Models;

public enum SaveStatus
{
    Unknown = 0,
    Valid = 1,
    Suspicious = 2,
    Corrupt = 3,
    Unsupported = 4,
    Empty = 5,
    Unreadable = 6
}

public sealed class SaveMetadata
{
    public string Format { get; set; } = string.Empty;
    public int SectionCount { get; set; }
    public List<string> SectionIds { get; set; } = new();
    public Dictionary<string, int> SectionLengths { get; set; } = new(StringComparer.Ordinal);
    public int? ReleaseEpisode { get; set; }
    public int? ReleaseBuild { get; set; }
    public bool HasThumbnail { get; set; }
    public bool HasBuildParts { get; set; }
    public bool ChecksumsVerified { get; set; }
    public int ChecksumFailures { get; set; }

    public string FormatDisplay => string.IsNullOrEmpty(Format) ? "Unknown format" : Format;

    public string ReleaseLabelDisplay
    {
        get
        {
            if (ReleaseEpisode is null && ReleaseBuild is null)
            {
                return "not embedded";
            }

            var episode = ReleaseEpisode is null ? "episode ?" : $"episode {ReleaseEpisode}";
            var build = ReleaseBuild is null ? "build ?" : $"build {ReleaseBuild}";
            return $"{episode}, {build}";
        }
    }
}

public sealed class SaveFileInfo
{
    public string Path { get; set; } = string.Empty;
    public Episode Episode { get; set; } = Episode.Unknown;
    public string? Folder { get; set; }
    public long Length { get; set; }
    public DateTime CreatedUtc { get; set; }
    public DateTime ModifiedUtc { get; set; }
    public string? Sha256 { get; set; }
    public SaveStatus Status { get; set; } = SaveStatus.Unknown;
    public string? StatusMessage { get; set; }
    public SaveMetadata Metadata { get; set; } = new();
    public List<string> SidecarPaths { get; set; } = new();

    public string FileName => System.IO.Path.GetFileName(Path);

    public string BaseName => System.IO.Path.GetFileNameWithoutExtension(Path);

    public string Extension => System.IO.Path.GetExtension(Path);

    public string SizeDisplay => Length switch
    {
        < 1024 => $"{Length} B",
        < 1024 * 1024 => $"{Length / 1024.0:0.0} KB",
        _ => $"{Length / (1024.0 * 1024.0):0.00} MB"
    };

    public string StatusDisplay => Status switch
    {
        SaveStatus.Valid => "Healthy",
        SaveStatus.Suspicious => "Check recommended",
        SaveStatus.Corrupt => "Corrupt or incomplete",
        SaveStatus.Unsupported => "Unsupported format",
        SaveStatus.Empty => "Empty file",
        SaveStatus.Unreadable => "Unreadable",
        _ => "Not checked"
    };

    public bool IsHealthy => Status == SaveStatus.Valid;
}
