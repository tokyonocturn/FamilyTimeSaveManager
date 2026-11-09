using FamilyTimeSaveManager.Core.Detection;
using FamilyTimeSaveManager.Core.Formats;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Services;

/// <summary>
/// Reads save folders and reports what is there. It never writes, renames or deletes a save.
/// </summary>
public sealed class SaveScanner
{
    private readonly ActivityLog _log;

    public SaveScanner(ActivityLog log)
    {
        _log = log;
    }

    public IReadOnlyList<SaveFileInfo> Scan(
        IEnumerable<SaveLocation> locations,
        bool verifyChecksums = true,
        CancellationToken cancellationToken = default)
    {
        var saves = new List<SaveFileInfo>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var location in locations)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!Directory.Exists(location.Path))
            {
                continue;
            }

            string[] files;
            try
            {
                files = Directory.GetFiles(location.Path, "*", SearchOption.TopDirectoryOnly);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                _log.Warn("scan", $"Could not list {location.Path}: {ex.Message}");
                continue;
            }

            foreach (var file in files)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var extension = Path.GetExtension(file);
                if (!extension.Equals(FamilySaveFormat.Episode3Extension, StringComparison.OrdinalIgnoreCase)
                    && !extension.Equals(FamilySaveFormat.LegacyExtension, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (!seen.Add(Path.GetFullPath(file)))
                {
                    continue;
                }

                try
                {
                    saves.Add(Inspect(file, location, verifyChecksums));
                }
                catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
                {
                    _log.Warn("scan", $"Could not read {file}: {ex.Message}");
                    saves.Add(new SaveFileInfo
                    {
                        Path = file,
                        Episode = location.Episode,
                        Folder = location.Path,
                        Status = SaveStatus.Unreadable,
                        StatusMessage = ex.Message
                    });
                }
            }
        }

        return saves
            .OrderByDescending(s => s.ModifiedUtc)
            .ToList();
    }

    public SaveFileInfo Inspect(string path, SaveLocation location, bool verifyChecksums = true)
    {
        var info = new FileInfo(path);
        var parsed = FamilySaveFormat.Parse(path, verifyChecksums);

        var episode = location.Episode;
        if (episode == Episode.Unknown && parsed.ReleaseEpisode is not null)
        {
            episode = EpisodeExtensions.FromNumber(parsed.ReleaseEpisode);
        }

        var status = parsed.Status;
        var message = parsed.Message;
        var extension = info.Extension.ToLowerInvariant();

        if (status == SaveStatus.Valid)
        {
            var extensionMatches = (parsed.Format == SaveFormatKind.Episode3Family && extension == FamilySaveFormat.Episode3Extension)
                                   || (parsed.Format == SaveFormatKind.LegacyWorld && extension == FamilySaveFormat.LegacyExtension);
            if (!extensionMatches)
            {
                status = SaveStatus.Suspicious;
                message = Combine(message, "The file extension does not match the container format.");
            }
        }

        if (parsed.ReleaseEpisode is not null
            && location.Episode != Episode.Unknown
            && parsed.ReleaseEpisode != (int)location.Episode)
        {
            status = status == SaveStatus.Corrupt ? status : SaveStatus.Suspicious;
            message = Combine(message, $"The save labels itself as Episode {parsed.ReleaseEpisode}, but it is stored with {location.Episode.ToDisplayName()} files.");
        }

        if (parsed.ReleaseBuild is not null
            && !string.IsNullOrWhiteSpace(location.BuildLabel)
            && int.TryParse(location.BuildLabel, out var folderBuild)
            && folderBuild != parsed.ReleaseBuild)
        {
            status = status == SaveStatus.Corrupt ? status : SaveStatus.Suspicious;
            message = Combine(message, $"The save was written by build {parsed.ReleaseBuild}, but this folder belongs to build {folderBuild}.");
        }

        var save = new SaveFileInfo
        {
            Path = info.FullName,
            Episode = episode,
            Folder = location.Path,
            Length = info.Length,
            CreatedUtc = info.CreationTimeUtc,
            ModifiedUtc = info.LastWriteTimeUtc,
            Sha256 = SafeHash(info.FullName),
            Status = status,
            StatusMessage = message,
            Metadata = new SaveMetadata
            {
                Format = parsed.Format switch
                {
                    SaveFormatKind.Episode3Family => "Episode 3 .family",
                    SaveFormatKind.LegacyWorld => "Legacy .world",
                    _ => "Unknown"
                },
                SectionCount = parsed.SectionIds.Count,
                SectionIds = parsed.SectionIds,
                SectionLengths = new Dictionary<string, int>(parsed.SectionLengths, StringComparer.Ordinal),
                ReleaseEpisode = parsed.ReleaseEpisode,
                ReleaseBuild = parsed.ReleaseBuild,
                HasThumbnail = parsed.HasThumbnail,
                ChecksumsVerified = parsed.ChecksumsVerified,
                ChecksumFailures = parsed.ChecksumFailures
            }
        };

        var sidecar = FindSidecar(info, location);
        if (sidecar is not null)
        {
            save.SidecarPaths.Add(sidecar);
            save.Metadata.HasBuildParts = true;
        }

        return save;
    }

    private static string? FindSidecar(FileInfo save, SaveLocation location)
    {
        var folder = location.SidecarFolder ?? location.Path;
        if (string.IsNullOrWhiteSpace(folder))
        {
            return null;
        }

        var candidate = Path.Combine(folder, save.Name is { Length: > 0 }
            ? Path.GetFileNameWithoutExtension(save.Name) + ".bpm"
            : save.Name + ".bpm");

        return File.Exists(candidate) ? Path.GetFullPath(candidate) : null;
    }

    private static string? SafeHash(string path)
    {
        try
        {
            return FileOps.ComputeSha256(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return null;
        }
    }

    private static string Combine(string? first, string? second)
    {
        if (string.IsNullOrWhiteSpace(first))
        {
            return second ?? string.Empty;
        }

        return string.IsNullOrWhiteSpace(second) ? first : first + " " + second;
    }
}
