namespace FamilyTimeSaveManager.Core.Models;

public enum HistoryAction
{
    Backup = 0,
    Restore = 1,
    Export = 2,
    Import = 3,
    Delete = 4,
    Verify = 5,
    Scan = 6,
    Schedule = 7,
    Settings = 8
}

public enum HistoryStatus
{
    Success = 0,
    Warning = 1,
    Failed = 2
}

public sealed class HistoryEntry
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public DateTime TimestampUtc { get; set; } = DateTime.UtcNow;
    public HistoryAction Action { get; set; } = HistoryAction.Scan;
    public HistoryStatus Status { get; set; } = HistoryStatus.Success;
    public string Summary { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public string? BackupId { get; set; }
    public string? SavePath { get; set; }
    public string? Sha256 { get; set; }
    public long? Bytes { get; set; }

    public string ActionDisplay => Action switch
    {
        HistoryAction.Backup => "Backup",
        HistoryAction.Restore => "Restore",
        HistoryAction.Export => "Export",
        HistoryAction.Import => "Import",
        HistoryAction.Delete => "Delete",
        HistoryAction.Verify => "Verify",
        HistoryAction.Scan => "Scan",
        HistoryAction.Schedule => "Schedule",
        HistoryAction.Settings => "Settings",
        _ => "Action"
    };

    public string StatusDisplay => Status switch
    {
        HistoryStatus.Success => "Success",
        HistoryStatus.Warning => "Warning",
        _ => "Failed"
    };
}
