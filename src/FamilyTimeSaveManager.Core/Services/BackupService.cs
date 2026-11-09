using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Services;

public sealed record BackupVerification(bool Ok, IReadOnlyList<string> Errors);

/// <summary>
/// Creates and verifies backups. A backup is only reported as successful once every copied file
/// has been re-hashed from disk and matches the source.
/// </summary>
public sealed class BackupService
{
    private readonly AppPaths _paths;
    private readonly BackupRepository _repository;
    private readonly HistoryStore _history;
    private readonly ActivityLog _log;

    public BackupService(AppPaths paths, BackupRepository repository, HistoryStore history, ActivityLog log)
    {
        _paths = paths;
        _repository = repository;
        _history = history;
        _log = log;
    }

    public BackupRecord Create(
        SaveFileInfo save,
        string? name,
        string? description,
        string? profileId,
        BackupKind kind,
        string? graphicsApiLabel = null,
        string? buildLabel = null)
    {
        ArgumentNullException.ThrowIfNull(save);

        if (!File.Exists(save.Path))
        {
            throw new FileNotFoundException("The save file no longer exists.", save.Path);
        }

        var id = Guid.NewGuid().ToString("N");
        var filesFolder = _paths.BackupFilesFolder(id);
        Directory.CreateDirectory(filesFolder);

        var record = new BackupRecord
        {
            Id = id,
            CreatedUtc = DateTime.UtcNow,
            Kind = kind,
            Name = string.IsNullOrWhiteSpace(name) ? DefaultName(save) : name!.Trim(),
            Description = string.IsNullOrWhiteSpace(description) ? null : description!.Trim(),
            ProfileId = string.IsNullOrWhiteSpace(profileId) ? Profile.DefaultId : profileId!,
            Episode = save.Episode,
            GraphicsApiLabel = graphicsApiLabel,
            BuildLabel = buildLabel ?? save.Metadata.ReleaseBuild?.ToString(),
            SourcePath = save.Path,
            SourceSha256 = save.Sha256,
            Verified = false
        };

        try
        {
            var sources = new List<(string Path, string Role)>
            {
                (save.Path, "primary")
            };
            sources.AddRange(save.SidecarPaths
                .Where(p => File.Exists(p))
                .Select(p => (p, "sidecar")));

            var usedNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var (sourcePath, role) in sources)
            {
                var storedName = UniqueStoredName(Path.GetFileName(sourcePath), usedNames);
                var destination = Path.Combine(filesFolder, storedName);
                var sourceHash = FileOps.ComputeSha256(sourcePath);

                FileOps.CopyFileAtomic(sourcePath, destination);

                var copiedHash = FileOps.ComputeSha256(destination);
                if (!string.Equals(sourceHash, copiedHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new IOException($"The copy of '{Path.GetFileName(sourcePath)}' did not match its source checksum.");
                }

                record.Files.Add(new BackupFileEntry
                {
                    StoredName = storedName,
                    OriginalFileName = Path.GetFileName(sourcePath),
                    OriginalPath = sourcePath,
                    Sha256 = copiedHash,
                    Length = new FileInfo(destination).Length,
                    LastWriteUtc = File.GetLastWriteTimeUtc(sourcePath),
                    Role = role
                });
            }

            record.TotalBytes = record.Files.Sum(f => f.Length);
            record.Verified = true;
            _repository.Save(record);

            _history.Add(new HistoryEntry
            {
                Action = HistoryAction.Backup,
                Status = HistoryStatus.Success,
                Summary = $"{KindWord(kind)} backup '{record.Name}' created",
                Detail = $"{record.Files.Count} file(s), {record.SizeDisplay}",
                BackupId = record.Id,
                SavePath = save.Path,
                Sha256 = record.PrimaryFile?.Sha256,
                Bytes = record.TotalBytes
            });

            _log.Info("backup", $"Created {KindWord(kind)} backup '{record.Name}' ({record.SizeDisplay}) from {save.Path}.");
            return record;
        }
        catch (Exception ex)
        {
            TryRemoveFolder(filesFolder);
            TryRemoveFolder(_paths.BackupFolder(id));

            _history.Add(new HistoryEntry
            {
                Action = HistoryAction.Backup,
                Status = HistoryStatus.Failed,
                Summary = $"Backup of '{save.FileName}' failed",
                Detail = ex.Message,
                SavePath = save.Path
            });

            _log.Error("backup", $"Backup of {save.Path} failed: {ex.Message}");
            throw;
        }
    }

    public BackupVerification Verify(BackupRecord record)
    {
        ArgumentNullException.ThrowIfNull(record);
        var errors = new List<string>();

        if (!BackupRepository.IsValidId(record.Id))
        {
            return new BackupVerification(false, new[] { "The backup identifier is not valid." });
        }

        var folder = _paths.BackupFilesFolder(record.Id);
        if (!Directory.Exists(folder))
        {
            return new BackupVerification(false, new[] { "The backup folder is missing." });
        }

        if (record.Files.Count == 0)
        {
            errors.Add("The manifest lists no files.");
        }

        foreach (var entry in record.Files)
        {
            var storedName = FileOps.SanitizeFileName(entry.StoredName, "file");
            var path = Path.Combine(folder, storedName);

            if (!FileOps.IsWithin(folder, path))
            {
                errors.Add($"'{entry.StoredName}' resolves outside the backup folder.");
                continue;
            }

            if (!File.Exists(path))
            {
                errors.Add($"'{entry.StoredName}' is missing.");
                continue;
            }

            var info = new FileInfo(path);
            if (info.Length != entry.Length)
            {
                errors.Add($"'{entry.StoredName}' is {info.Length} bytes but the manifest records {entry.Length}.");
                continue;
            }

            string hash;
            try
            {
                hash = FileOps.ComputeSha256(path);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                errors.Add($"'{entry.StoredName}' could not be read: {ex.Message}");
                continue;
            }

            if (!string.Equals(hash, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            {
                errors.Add($"'{entry.StoredName}' failed its SHA-256 check.");
            }
        }

        return new BackupVerification(errors.Count == 0, errors);
    }

    public static string DefaultName(SaveFileInfo save) =>
        $"{save.BaseName} ({DateTime.Now:yyyy-MM-dd HH-mm-ss})";

    private static string UniqueStoredName(string fileName, HashSet<string> used)
    {
        var candidate = FileOps.SanitizeFileName(fileName, "save");
        if (used.Add(candidate))
        {
            return candidate;
        }

        var stem = Path.GetFileNameWithoutExtension(candidate);
        var extension = Path.GetExtension(candidate);
        for (var index = 2; index < 1000; index++)
        {
            var next = $"{stem}-{index}{extension}";
            if (used.Add(next))
            {
                return next;
            }
        }

        throw new IOException("Could not allocate a unique file name for the backup.");
    }

    private static string KindWord(BackupKind kind) => kind switch
    {
        BackupKind.Automatic => "Scheduled",
        BackupKind.PreRestore => "Pre-restore",
        BackupKind.Imported => "Imported",
        _ => "Manual"
    };

    private void TryRemoveFolder(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, recursive: true);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            _log.Warn("backup", $"Could not remove incomplete backup folder {path}: {ex.Message}");
        }
    }
}
