using FamilyTimeSaveManager.Core.Detection;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Detection;

public sealed record SaveLocation(
    string Path,
    Episode Episode,
    string Source,
    string? InstallPath = null,
    string? SidecarFolder = null,
    string? BuildLabel = null,
    string? Note = null)
{
    public bool Exists => Directory.Exists(Path);
}

/// <summary>
/// Maps installations to the folders Family Time actually writes saves into:
/// Episode 3 uses &lt;install&gt;\FamilyTime_Data\Sessions, while Episodes 1 and 2 use the shared
/// Unity persistent data folder. A manual override always wins.
/// </summary>
public sealed class SaveLocationService
{
    private readonly SettingsStore _settings;

    public SaveLocationService(SettingsStore settings)
    {
        _settings = settings;
    }

    public IReadOnlyList<SaveLocation> Resolve(IReadOnlyList<GameInstall> installs)
    {
        var locations = new List<SaveLocation>();
        var settings = _settings.Current;

        for (var episode = Episode.Episode1; episode <= Episode.Episode3; episode++)
        {
            var overridePath = settings.GetSavePathOverride(episode);
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                var sidecars = ResolvePersistentFolder(installs);
                locations.Add(new SaveLocation(
                    overridePath!,
                    episode,
                    "Manual override",
                    installs.FirstOrDefault(i => i.Episode == episode)?.InstallPath,
                    episode == Episode.Episode3 ? sidecars : overridePath,
                    null,
                    "This folder was set manually, so saves here are treated as this episode."));
            }
        }

        foreach (var install in installs.Where(i => i.Episode == Episode.Episode3))
        {
            var sessions = install.SessionsFolder;
            if (sessions is null)
            {
                continue;
            }

            locations.Add(new SaveLocation(
                sessions,
                Episode.Episode3,
                "Detected install",
                install.InstallPath,
                ResolvePersistentFolder(installs),
                install.BuildLabel,
                install.BuildLabel is null ? null : $"Episode 3 build {install.BuildLabel}"));
        }

        var legacyInstalls = installs.Where(i => i.Episode is Episode.Episode1 or Episode.Episode2).ToList();
        var persistentFolder = ResolvePersistentFolder(installs);
        if (persistentFolder is not null)
        {
            var distinctLegacyEpisodes = legacyInstalls.Select(i => i.Episode).Distinct().ToList();
            if (distinctLegacyEpisodes.Count == 1)
            {
                locations.Add(new SaveLocation(
                    persistentFolder,
                    distinctLegacyEpisodes[0],
                    "Detected install",
                    legacyInstalls.FirstOrDefault()?.InstallPath,
                    persistentFolder,
                    legacyInstalls.FirstOrDefault()?.BuildLabel,
                    null));
            }
            else if (distinctLegacyEpisodes.Count > 1)
            {
                locations.Add(new SaveLocation(
                    persistentFolder,
                    Episode.Unknown,
                    "Shared legacy folder",
                    null,
                    persistentFolder,
                    null,
                    "Episodes 1 and 2 both write .world saves to this folder, and the format does not embed an episode number, so these saves cannot be attributed to one episode."));
            }
        }

        return locations
            .GroupBy(l => (Normalize(l.Path), l.Episode))
            .Select(g => g.First())
            .OrderBy(l => l.Episode)
            .ThenBy(l => l.Path, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public string? ResolvePersistentFolder(IReadOnlyList<GameInstall> installs)
    {
        var install = installs.FirstOrDefault(i => i.CompanyName is not null || i.ProductName is not null);
        return install is null
            ? UnityPathResolver.PersistentDataPath(null)
            : UnityPathResolver.PersistentDataPath(install.CompanyName, install.ProductName);
    }

    private static string Normalize(string path)
    {
        try
        {
            return Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return path;
        }
    }
}
