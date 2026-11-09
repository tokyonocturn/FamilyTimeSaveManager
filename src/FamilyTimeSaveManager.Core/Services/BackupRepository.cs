using System.Text.Json;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Services;

/// <summary>
/// Backup folders are self-describing: each one holds manifest.json and a files folder whose
/// entries are verified against the manifest's SHA-256 values.
/// </summary>
public sealed class BackupRepository
{
    private readonly AppPaths _paths;
    private readonly ActivityLog _log;

    public BackupRepository(AppPaths paths, ActivityLog log)
    {
        _paths = paths;
        _log = log;
        Directory.CreateDirectory(paths.BackupsRoot);
    }

    public string FolderFor(string id) => _paths.BackupFolder(id);

    public IReadOnlyList<BackupRecord> List()
    {
        var records = new List<BackupRecord>();
        string[] folders;
        try
        {
            folders = Directory.GetDirectories(_paths.BackupsRoot);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("backups", "Could not list the backup folder: " + ex.Message);
            return records;
        }

        foreach (var folder in folders)
        {
            if (Path.GetFileName(folder).StartsWith('.'))
            {
                continue;
            }

            var record = TryReadManifest(Path.Combine(folder, "manifest.json"));
            if (record is null)
            {
                _log.Warn("backups", $"Ignoring unreadable backup folder '{Path.GetFileName(folder)}'.");
                continue;
            }

            records.Add(record);
        }

        return records
            .OrderByDescending(r => r.CreatedUtc)
            .ToList();
    }

    public BackupRecord? Get(string id)
    {
        if (!IsValidId(id))
        {
            return null;
        }

        return TryReadManifest(_paths.BackupManifestFile(id));
    }

    public void Save(BackupRecord record)
    {
        if (!IsValidId(record.Id))
        {
            throw new ArgumentException("Backup identifier is not valid.", nameof(record));
        }

        JsonFile.Save(_paths.BackupManifestFile(record.Id), record);
    }

    /// <summary>
    /// Removes a backup from the library by moving its folder into a trash folder. Nothing is
    /// destroyed by accident, and original save files are never involved.
    /// </summary>
    public void MoveToTrash(string id)
    {
        if (!IsValidId(id))
        {
            throw new ArgumentException("Backup identifier is not valid.", nameof(id));
        }

        var folder = _paths.BackupFolder(id);
        if (!Directory.Exists(folder))
        {
            return;
        }

        var trashRoot = Path.Combine(_paths.BackupsRoot, ".trash");
        Directory.CreateDirectory(trashRoot);
        var target = Path.Combine(trashRoot, $"{id}-{DateTime.UtcNow:yyyyMMddHHmmss}");
        Directory.Move(folder, target);
        _log.Info("backups", $"Moved backup {id} to trash.");
    }

    public static bool IsValidId(string? id) =>
        id is { Length: 32 } && id.All(c => c is >= '0' and <= '9' or >= 'a' and <= 'f');

    private BackupRecord? TryReadManifest(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            if (string.IsNullOrWhiteSpace(json))
            {
                return null;
            }

            var record = JsonSerializer.Deserialize<BackupRecord>(json, JsonFile.Options);
            if (record is null || !IsValidId(record.Id))
            {
                return null;
            }

            return record;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }
}
