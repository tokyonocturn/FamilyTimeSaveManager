namespace FamilyTimeSaveManager.Core.Models;

public enum BackupKind
{
    Manual = 0,
    Automatic = 1,
    PreRestore = 2,
    Imported = 3
}

public sealed class BackupFileEntry
{
    /// <summary>File name inside the backup's files folder.</summary>
    public string StoredName { get; set; } = string.Empty;

    public string OriginalFileName { get; set; } = string.Empty;

    public string OriginalPath { get; set; } = string.Empty;

    public string Sha256 { get; set; } = string.Empty;

    public long Length { get; set; }

    public DateTime LastWriteUtc { get; set; }

    /// <summary>"primary" for the save itself, "sidecar" for related build-part data.</summary>
    public string Role { get; set; } = "primary";
}

public sealed class BackupRecord
{
    public string Id { get; set; } = string.Empty;
    public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    public BackupKind Kind { get; set; } = BackupKind.Manual;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ProfileId { get; set; } = Profile.DefaultId;
    public Episode Episode { get; set; } = Episode.Unknown;
    public string? GraphicsApiLabel { get; set; }
    public string? BuildLabel { get; set; }
    public string? SourcePath { get; set; }
    public string? SourceSha256 { get; set; }
    public long TotalBytes { get; set; }
    public bool Verified { get; set; }
    public string? ImportedFrom { get; set; }
    public List<BackupFileEntry> Files { get; set; } = new();

    public BackupFileEntry? PrimaryFile => Files.FirstOrDefault(f => f.Role == "primary") ?? Files.FirstOrDefault();

    public string SizeDisplay => TotalBytes switch
    {
        < 1024 => $"{TotalBytes} B",
        < 1024 * 1024 => $"{TotalBytes / 1024.0:0.0} KB",
        _ => $"{TotalBytes / (1024.0 * 1024.0):0.00} MB"
    };

    public string KindDisplay => Kind switch
    {
        BackupKind.Manual => "Manual",
        BackupKind.Automatic => "Scheduled",
        BackupKind.PreRestore => "Pre-restore",
        BackupKind.Imported => "Imported",
        _ => "Backup"
    };

    public string EpisodeDisplay => Episode == Episode.Unknown ? "Unknown episode" : Episode.ToDisplayName();
}
