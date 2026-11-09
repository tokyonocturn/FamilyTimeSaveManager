using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.Core.Storage;

public sealed class HistoryStore
{
    private const int MaxEntries = 5000;
    private readonly object _gate = new();
    private readonly string _path;
    private List<HistoryEntry> _entries;

    public HistoryStore(AppPaths paths)
    {
        _path = paths.HistoryFile;
        _entries = JsonFile.Load(_path, () => new List<HistoryEntry>());
    }

    public void Add(HistoryEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        lock (_gate)
        {
            _entries.Insert(0, entry);
            if (_entries.Count > MaxEntries)
            {
                _entries.RemoveRange(MaxEntries, _entries.Count - MaxEntries);
            }

            Persist();
        }
    }

    public IReadOnlyList<HistoryEntry> Search(string? text, HistoryAction? action = null, HistoryStatus? status = null, int limit = 1000)
    {
        lock (_gate)
        {
            IEnumerable<HistoryEntry> query = _entries;

            if (!string.IsNullOrWhiteSpace(text))
            {
                var needle = text.Trim();
                query = query.Where(e => Contains(e.Summary, needle)
                                         || Contains(e.Detail, needle)
                                         || Contains(e.SavePath, needle)
                                         || Contains(e.BackupId, needle)
                                         || Contains(e.Sha256, needle)
                                         || Contains(e.ActionDisplay, needle)
                                         || Contains(e.StatusDisplay, needle));
            }

            if (action is not null)
            {
                query = query.Where(e => e.Action == action);
            }

            if (status is not null)
            {
                query = query.Where(e => e.Status == status);
            }

            return query.Take(Math.Max(1, limit)).ToList();
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _entries.Count;
            }
        }
    }

    private void Persist() => JsonFile.Save(_path, _entries);

    private static bool Contains(string? value, string needle) =>
        value is not null && value.Contains(needle, StringComparison.OrdinalIgnoreCase);
}
