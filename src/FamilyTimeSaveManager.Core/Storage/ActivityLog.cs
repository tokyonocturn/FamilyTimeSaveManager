namespace FamilyTimeSaveManager.Core.Storage;

public sealed record LogEntry(DateTime TimestampUtc, string Level, string Area, string Message)
{
    public string Display => $"{TimestampUtc.ToLocalTime():HH:mm:ss}  {Level.ToUpperInvariant(),-5}  {Area,-10}  {Message}";
}

public sealed class ActivityLog : IDisposable
{
    private readonly object _gate = new();
    private readonly string _directory;
    private readonly string _filePath;
    private bool _disposed;

    public ActivityLog(AppPaths paths)
    {
        _directory = paths.LogsRoot;
        Directory.CreateDirectory(_directory);
        _filePath = Path.Combine(_directory, $"activity-{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public event Action<LogEntry>? EntryWritten;

    public string FilePath => _filePath;

    public void Info(string area, string message) => Write("info", area, message);

    public void Warn(string area, string message) => Write("warn", area, message);

    public void Error(string area, string message) => Write("error", area, message);

    public void Write(string level, string area, string message)
    {
        var entry = new LogEntry(DateTime.UtcNow, level, area, message);
        lock (_gate)
        {
            if (_disposed)
            {
                return;
            }

            try
            {
                File.AppendAllText(_filePath, entry.Display + Environment.NewLine);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        try
        {
            EntryWritten?.Invoke(entry);
        }
        catch
        {
            // A subscriber must never break logging.
        }
    }

    public void Dispose() => _disposed = true;
}
