using System.IO.Compression;
using System.Text.Json;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Services;

public sealed record ArchiveExportResult(string ArchivePath, int BackupCount, long ArchiveBytes);

public sealed record ArchiveImportResult(IReadOnlyList<BackupRecord> Imported, string Message);

public sealed class ArchiveException : Exception
{
    public ArchiveException(string message) : base(message)
    {
    }

    public ArchiveException(string message, Exception inner) : base(message, inner)
    {
    }
}

/// <summary>
/// Exports verified backups into a ZIP archive and imports them back. Import treats the archive as
/// hostile: entry paths are normalised, parent traversal and absolute paths are rejected, symlink
/// entries are rejected, size and entry counts are bounded, and every file is hashed against the
/// manifest before anything is added to the library.
/// </summary>
public sealed class ArchiveService
{
    private const int FormatVersion = 1;
    private const int MaxEntries = 5000;
    private const long MaxEntryBytes = 2L * 1024 * 1024 * 1024;
    private const long MaxTotalBytes = 8L * 1024 * 1024 * 1024;

    private readonly AppPaths _paths;
    private readonly BackupRepository _repository;
    private readonly BackupService _backups;
    private readonly HistoryStore _history;
    private readonly ActivityLog _log;

    public ArchiveService(
        AppPaths paths,
        BackupRepository repository,
        BackupService backups,
        HistoryStore history,
        ActivityLog log)
    {
        _paths = paths;
        _repository = repository;
        _backups = backups;
        _history = history;
        _log = log;
    }

    public ArchiveExportResult Export(IReadOnlyList<BackupRecord> backups, string destinationPath, bool overwrite = false)
    {
        ArgumentNullException.ThrowIfNull(backups);
        if (backups.Count == 0)
        {
            throw new ArchiveException("Select at least one backup to export.");
        }

        if (string.IsNullOrWhiteSpace(destinationPath))
        {
            throw new ArchiveException("Choose where to save the archive.");
        }

        var destination = Path.GetFullPath(destinationPath);
        var directory = Path.GetDirectoryName(destination)
                        ?? throw new ArchiveException("The archive destination is not a file path.");
        Directory.CreateDirectory(directory);

        if (File.Exists(destination) && !overwrite)
        {
            throw new ArchiveException("An archive already exists at that location.");
        }

        var tempPath = Path.Combine(directory, $".export-{Guid.NewGuid():N}.zip");
        var verified = new List<BackupRecord>();

        try
        {
            foreach (var record in backups)
            {
                var check = _backups.Verify(record);
                if (!check.Ok)
                {
                    throw new ArchiveException(
                        $"Backup '{record.Name}' failed verification and was not exported: {string.Join(" ", check.Errors)}");
                }

                verified.Add(record);
            }

            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Create))
            {
                WriteEntry(archive, "bundle.json", JsonSerializer.Serialize(new BundleManifest
                {
                    FormatVersion = FormatVersion,
                    ExportedUtc = DateTime.UtcNow,
                    BackupIds = verified.Select(b => b.Id).ToList()
                }, JsonFile.Options));

                foreach (var record in verified)
                {
                    WriteEntry(archive, $"{record.Id}/manifest.json", JsonSerializer.Serialize(record, JsonFile.Options));

                    var filesFolder = _paths.BackupFilesFolder(record.Id);
                    foreach (var entry in record.Files)
                    {
                        var storedName = FileOps.SanitizeFileName(entry.StoredName, "file");
                        var source = Path.Combine(filesFolder, storedName);
                        if (!FileOps.IsWithin(filesFolder, source) || !File.Exists(source))
                        {
                            throw new ArchiveException($"Backup '{record.Name}' is missing '{entry.StoredName}'.");
                        }

                        var zipEntry = archive.CreateEntry($"{record.Id}/files/{storedName}", CompressionLevel.Optimal);
                        using var entryStream = zipEntry.Open();
                        using var sourceStream = new FileStream(source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                        sourceStream.CopyTo(entryStream);
                    }
                }
            }

            if (File.Exists(destination))
            {
                File.Replace(tempPath, destination, null, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, destination);
            }

            var bytes = new FileInfo(destination).Length;
            _history.Add(new HistoryEntry
            {
                Action = HistoryAction.Export,
                Status = HistoryStatus.Success,
                Summary = $"Exported {verified.Count} backup(s)",
                Detail = destination,
                Bytes = bytes
            });

            _log.Info("archive", $"Exported {verified.Count} backup(s) to {destination} ({bytes} bytes).");
            return new ArchiveExportResult(destination, verified.Count, bytes);
        }
        catch (Exception ex)
        {
            FileOps.TryDelete(tempPath);
            _history.Add(new HistoryEntry
            {
                Action = HistoryAction.Export,
                Status = HistoryStatus.Failed,
                Summary = "Backup export failed",
                Detail = ex.Message
            });
            _log.Error("archive", "Export failed: " + ex.Message);
            throw;
        }
    }

    public ArchiveImportResult Import(string archivePath)
    {
        if (string.IsNullOrWhiteSpace(archivePath) || !File.Exists(archivePath))
        {
            throw new ArchiveException("The archive file was not found.");
        }

        var tempRoot = Path.Combine(_paths.ExportsRoot, "import-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);

        try
        {
            using (var stream = new FileStream(archivePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                if (archive.Entries.Count > MaxEntries)
                {
                    throw new ArchiveException($"The archive contains {archive.Entries.Count} entries, which is more than the supported {MaxEntries}.");
                }

                var total = 0L;
                foreach (var entry in archive.Entries)
                {
                    var relative = NormalizeEntryName(entry.FullName, out var isDirectory);
                    if (relative is null)
                    {
                        continue;
                    }

                    if (entry.Length > MaxEntryBytes)
                    {
                        throw new ArchiveException($"'{entry.FullName}' is larger than the supported {MaxEntryBytes / (1024 * 1024)} MB per file.");
                    }

                    total += entry.Length;
                    if (total > MaxTotalBytes)
                    {
                        throw new ArchiveException("The archive expands to more than the supported 8 GB.");
                    }

                    if (entry.Length > 1024 * 1024
                        && entry.CompressedLength > 0
                        && entry.Length / Math.Max(1, entry.CompressedLength) > 1000)
                    {
                        throw new ArchiveException($"'{entry.FullName}' has an implausible compression ratio and was rejected.");
                    }

                    var target = Path.Combine(tempRoot, relative.Replace('/', Path.DirectorySeparatorChar));
                    if (!FileOps.IsWithin(tempRoot, target))
                    {
                        throw new ArchiveException($"'{entry.FullName}' would be written outside the import folder.");
                    }

                    if (isDirectory)
                    {
                        Directory.CreateDirectory(target);
                        continue;
                    }

                    var parent = Path.GetDirectoryName(target);
                    if (!string.IsNullOrEmpty(parent))
                    {
                        Directory.CreateDirectory(parent);
                    }

                    using var entryStream = entry.Open();
                    using var targetStream = new FileStream(target, FileMode.CreateNew, FileAccess.Write, FileShare.None);
                    entryStream.CopyTo(targetStream);
                }
            }

            var bundle = TryReadBundle(tempRoot);
            var candidates = Directory.GetFiles(tempRoot, "manifest.json", SearchOption.AllDirectories);
            if (candidates.Length == 0)
            {
                throw new ArchiveException("The archive does not contain any backup manifests.");
            }

            var verified = new List<(BackupRecord Record, string Folder)>();
            foreach (var manifestPath in candidates)
            {
                var folder = Path.GetDirectoryName(manifestPath)!;
                if (string.Equals(folder, tempRoot, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                BackupRecord record;
                try
                {
                    record = JsonSerializer.Deserialize<BackupRecord>(File.ReadAllText(manifestPath), JsonFile.Options)
                             ?? throw new ArchiveException("A manifest in the archive is empty.");
                }
                catch (JsonException ex)
                {
                    throw new ArchiveException("A backup manifest in the archive is not valid JSON.", ex);
                }

                if (!BackupRepository.IsValidId(record.Id))
                {
                    throw new ArchiveException($"The archive contains an invalid backup identifier '{record.Id}'.");
                }

                var filesFolder = Path.Combine(folder, "files");
                if (!Directory.Exists(filesFolder))
                {
                    throw new ArchiveException($"Backup '{record.Name}' has no files folder.");
                }

                if (record.Files.Count == 0)
                {
                    throw new ArchiveException($"Backup '{record.Name}' lists no files.");
                }

                foreach (var entry in record.Files)
                {
                    var storedName = FileOps.SanitizeFileName(entry.StoredName, "file");
                    if (!string.Equals(storedName, entry.StoredName, StringComparison.Ordinal))
                    {
                        throw new ArchiveException($"Backup '{record.Name}' contains an unsafe file name.");
                    }

                    var path = Path.Combine(filesFolder, storedName);
                    if (!FileOps.IsWithin(filesFolder, path) || !File.Exists(path))
                    {
                        throw new ArchiveException($"Backup '{record.Name}' is missing '{storedName}'.");
                    }

                    var info = new FileInfo(path);
                    if (info.Length != entry.Length)
                    {
                        throw new ArchiveException($"Backup '{record.Name}' file '{storedName}' does not match its recorded size.");
                    }

                    if (!string.Equals(FileOps.ComputeSha256(path), entry.Sha256, StringComparison.OrdinalIgnoreCase))
                    {
                        throw new ArchiveException($"Backup '{record.Name}' file '{storedName}' failed its SHA-256 check.");
                    }
                }

                verified.Add((record, folder));
            }

            var imported = new List<BackupRecord>();
            foreach (var (record, folder) in verified)
            {
                var newId = Guid.NewGuid().ToString("N");
                var targetFolder = _paths.BackupFolder(newId);
                Directory.Move(folder, targetFolder);

                record.Id = newId;
                record.Kind = BackupKind.Imported;
                record.ImportedFrom = Path.GetFileName(archivePath);
                record.Verified = true;
                record.ProfileId = string.IsNullOrWhiteSpace(record.ProfileId) ? Profile.DefaultId : record.ProfileId;

                _repository.Save(record);
                imported.Add(record);
            }

            var detail = bundle is null
                ? $"{imported.Count} backup(s) imported."
                : $"{imported.Count} backup(s) imported from a version {bundle.FormatVersion} archive.";

            _history.Add(new HistoryEntry
            {
                Action = HistoryAction.Import,
                Status = HistoryStatus.Success,
                Summary = $"Imported {imported.Count} backup(s)",
                Detail = detail,
                Bytes = imported.Sum(i => i.TotalBytes)
            });

            _log.Info("archive", $"Imported {imported.Count} verified backup(s) from {archivePath}.");
            return new ArchiveImportResult(imported, detail);
        }
        catch (Exception ex)
        {
            _history.Add(new HistoryEntry
            {
                Action = HistoryAction.Import,
                Status = HistoryStatus.Failed,
                Summary = "Backup import failed",
                Detail = ex.Message
            });
            _log.Error("archive", "Import failed: " + ex.Message);
            throw;
        }
        finally
        {
            try
            {
                if (Directory.Exists(tempRoot))
                {
                    Directory.Delete(tempRoot, recursive: true);
                }
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn("archive", "Could not remove the import staging folder: " + ex.Message);
            }
        }
    }

    /// <summary>
    /// Accepts only relative, normalised ZIP entry names. Returns null for directory entries that
    /// are already represented, and throws for anything that could escape the import folder.
    /// </summary>
    private static string? NormalizeEntryName(string? fullName, out bool isDirectory)
    {
        isDirectory = false;
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var normalized = fullName.Replace('\\', '/').Trim();
        isDirectory = normalized.EndsWith("/", StringComparison.Ordinal);
        normalized = normalized.TrimEnd('/');

        if (normalized.Length == 0)
        {
            return null;
        }

        if (normalized.StartsWith("/", StringComparison.Ordinal)
            || (normalized.Length >= 2 && char.IsLetter(normalized[0]) && normalized[1] == ':')
            || normalized.Contains('\0'))
        {
            throw new ArchiveException($"'{fullName}' is an absolute path and was rejected.");
        }

        var segments = normalized.Split('/');
        if (segments.Any(s => s.Length == 0 || s == ".." || s == "."))
        {
            throw new ArchiveException($"'{fullName}' contains a parent or empty path segment and was rejected.");
        }

        return string.Join('/', segments);
    }

    private BundleManifest? TryReadBundle(string root)
    {
        var path = Path.Combine(root, "bundle.json");
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            var bundle = JsonSerializer.Deserialize<BundleManifest>(File.ReadAllText(path), JsonFile.Options);
            if (bundle is not null && bundle.FormatVersion > FormatVersion)
            {
                throw new ArchiveException($"The archive uses bundle format {bundle.FormatVersion}, which this version cannot read.");
            }

            return bundle;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        using var writer = new StreamWriter(stream);
        writer.Write(content);
    }

    private sealed class BundleManifest
    {
        public int FormatVersion { get; set; }
        public DateTime ExportedUtc { get; set; }
        public List<string> BackupIds { get; set; } = new();
    }
}
