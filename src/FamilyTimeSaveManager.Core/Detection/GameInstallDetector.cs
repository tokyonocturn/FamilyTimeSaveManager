using System.Diagnostics;
using System.Text;
using System.Text.RegularExpressions;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Detection;

/// <summary>
/// Finds Family Time installations by looking for a folder that contains FamilyTime.exe and
/// FamilyTime_Data. Every conclusion is recorded with the evidence that produced it, and nothing
/// is written to the install folder.
/// </summary>
public sealed class GameInstallDetector
{
    private static readonly string[] SkippedDirectoryNames =
    {
        "windows", "$recycle.bin", "system volume information", "appdata", "node_modules", ".git",
        "recovery", "perflogs", "msocache", "config.msi", "programdata", ".cache", "packages"
    };

    private static readonly string[] EpisodeOneTokens = { "episode 1", "episode1", "ep_1", "ep-1", "ep1" };
    private static readonly string[] EpisodeTwoTokens = { "episode 2", "episode2", "ep_2", "ep-2", "ep2" };
    private static readonly string[] EpisodeThreeTokens = { "episode 3", "episode3", "ep_3", "ep-3", "ep3" };

    private static readonly Regex BuildToken = new(@"(?<![a-z0-9])(?:b|build[-_ ]?)(\d{1,4})(?![a-z0-9])", RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private readonly ActivityLog _log;
    private readonly SettingsStore? _settings;

    public GameInstallDetector(ActivityLog log, SettingsStore? settings = null)
    {
        _log = log;
        _settings = settings;
    }

    public IReadOnlyList<GameInstall> Detect(IEnumerable<string>? extraPaths = null, CancellationToken cancellationToken = default)
    {
        var results = new List<GameInstall>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var stopwatch = Stopwatch.StartNew();
        var visited = 0;
        const int maxDirectories = 20000;
        var truncated = false;

        var explicitPaths = new List<string>();
        if (extraPaths is not null)
        {
            explicitPaths.AddRange(extraPaths);
        }

        if (_settings is not null)
        {
            explicitPaths.AddRange(_settings.Current.ExtraInstallPaths);
        }

        foreach (var path in explicitPaths.Where(p => !string.IsNullOrWhiteSpace(p)))
        {
            foreach (var candidate in ExpandExplicitPath(path))
            {
                if (!LooksLikeInstall(candidate))
                {
                    continue;
                }

                var install = Inspect(candidate, "configured path");
                if (install is not null && seen.Add(install.InstallPath))
                {
                    results.Add(install);
                }
            }
        }

        var queue = new Queue<(string Path, int Depth)>();
        foreach (var root in CandidateRoots())
        {
            if (Directory.Exists(root))
            {
                queue.Enqueue((root, 0));
            }
        }

        while (queue.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (visited >= maxDirectories || stopwatch.Elapsed > TimeSpan.FromSeconds(20))
            {
                truncated = true;
                break;
            }

            var (current, depth) = queue.Dequeue();
            visited++;

            if (LooksLikeInstall(current))
            {
                var install = Inspect(current, "scan");
                if (install is not null && seen.Add(install.InstallPath))
                {
                    results.Add(install);
                    _log.Info("detect", $"Found {install.DisplayName} at {install.InstallPath}");
                }
            }

            if (depth >= 5)
            {
                continue;
            }

            string[] children;
            try
            {
                children = Directory.GetDirectories(current);
            }
            catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
            {
                continue;
            }

            foreach (var child in children)
            {
                var name = Path.GetFileName(child);
                if (SkippedDirectoryNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                try
                {
                    var attributes = File.GetAttributes(child);
                    if ((attributes & FileAttributes.ReparsePoint) != 0 || (attributes & FileAttributes.System) != 0)
                    {
                        continue;
                    }
                }
                catch (IOException)
                {
                    continue;
                }

                queue.Enqueue((child, depth + 1));
            }
        }

        if (truncated)
        {
            _log.Warn("detect", $"Automatic scan stopped after {visited} folders. Add an install folder in Settings to search a specific location.");
        }

        _log.Info("detect", $"Scan complete: {results.Count} installation(s) from {visited} folders in {stopwatch.Elapsed.TotalSeconds:0.0}s.");

        return results
            .OrderBy(i => i.Episode)
            .ThenBy(i => i.InstallPath, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public GameInstall? Inspect(string installPath, string source = "scan")
    {
        if (string.IsNullOrWhiteSpace(installPath))
        {
            return null;
        }

        string fullPath;
        try
        {
            fullPath = Path.GetFullPath(installPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            return null;
        }

        if (!LooksLikeInstall(fullPath))
        {
            return null;
        }

        var install = new GameInstall { InstallPath = fullPath, DetectionSource = source };
        ReadAppInfo(install);
        ReadExeVersion(install);
        ReadAssemblyMarkers(install);

        var episodeHint = InferEpisodeFromText(fullPath);
        var buildHint = InferBuildFromText(fullPath);
        install.BuildLabel = buildHint;

        var hasFamily = install.Evidence.Any(e => e.Contains("Assembly-CSharp contains .family", StringComparison.OrdinalIgnoreCase));
        if (hasFamily)
        {
            install.Episode = Episode.Episode3;
            install.Evidence.Add("Episode 3 identified from the .family save container in Assembly-CSharp.dll.");
        }
        else if (episodeHint != Episode.Unknown)
        {
            install.Episode = episodeHint;
            install.EpisodeIsGuessed = true;
            install.Evidence.Add($"Episode {episodeHint.ToDisplayName()} inferred from the folder name; the assembly only uses the legacy .world format.");
        }
        else
        {
            install.Episode = Episode.Unknown;
            install.EpisodeIsGuessed = true;
            install.Evidence.Add("Episode could not be identified. Use the legacy .world format, so it is Episode 1 or 2; assign it manually in Settings.");
        }

        install.GraphicsApi = InferGraphicsApi(fullPath, out var apiGuessed);
        install.GraphicsApiIsGuessed = apiGuessed;

        ApplyOverrides(install);

        return install;
    }

    public static bool LooksLikeInstall(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path))
        {
            return false;
        }

        return File.Exists(Path.Combine(path, "FamilyTime.exe"))
               && Directory.Exists(Path.Combine(path, "FamilyTime_Data"));
    }

    public static IReadOnlyList<string> CandidateRoots()
    {
        var roots = new List<string>();
        void Add(Environment.SpecialFolder folder)
        {
            var path = Environment.GetFolderPath(folder);
            if (!string.IsNullOrWhiteSpace(path))
            {
                roots.Add(path);
            }
        }

        Add(Environment.SpecialFolder.UserProfile);
        Add(Environment.SpecialFolder.DesktopDirectory);
        Add(Environment.SpecialFolder.MyDocuments);
        Add(Environment.SpecialFolder.ProgramFiles);
        Add(Environment.SpecialFolder.ProgramFilesX86);

        var localPrograms = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "Programs");
        if (Directory.Exists(localPrograms))
        {
            roots.Add(localPrograms);
        }

        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                if (drive.IsReady && drive.DriveType is DriveType.Fixed or DriveType.Removable)
                {
                    roots.Add(drive.RootDirectory.FullName);
                }
            }
        }
        catch (IOException)
        {
        }

        return roots.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static IEnumerable<string> ExpandExplicitPath(string path)
    {
        string? full = null;
        try
        {
            full = Path.GetFullPath(path);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
        }

        if (full is null)
        {
            yield break;
        }

        if (File.Exists(Path.Combine(full, "FamilyTime.exe")))
        {
            yield return full;
        }

        string[] children;
        try
        {
            children = Directory.Exists(full) ? Directory.GetDirectories(full) : Array.Empty<string>();
        }
        catch (Exception ex) when (ex is UnauthorizedAccessException or IOException)
        {
            children = Array.Empty<string>();
        }

        foreach (var child in children)
        {
            if (File.Exists(Path.Combine(child, "FamilyTime.exe")))
            {
                yield return child;
            }
        }
    }

    private void ReadAppInfo(GameInstall install)
    {
        var path = Path.Combine(install.DataPath, "app.info");
        try
        {
            if (!File.Exists(path))
            {
                install.Evidence.Add("app.info was not found, so the default company and product folders are assumed.");
                return;
            }

            var lines = File.ReadAllLines(path);
            if (lines.Length >= 1 && !string.IsNullOrWhiteSpace(lines[0]))
            {
                install.CompanyName = lines[0].Trim();
            }

            if (lines.Length >= 2 && !string.IsNullOrWhiteSpace(lines[1]))
            {
                install.ProductName = lines[1].Trim();
            }

            install.Evidence.Add($"app.info reports company '{install.CompanyName}' and product '{install.ProductName}'.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            install.Evidence.Add("app.info could not be read: " + ex.Message);
        }
    }

    private static void ReadExeVersion(GameInstall install)
    {
        try
        {
            var info = FileVersionInfo.GetVersionInfo(install.ExePath);
            var product = info.ProductVersion;
            if (!string.IsNullOrWhiteSpace(product))
            {
                var space = product.IndexOf(' ');
                install.UnityVersion = space > 0 ? product[..space] : product;
                install.Evidence.Add($"Player was built with Unity {install.UnityVersion}.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException
                                   or InvalidOperationException or BadImageFormatException or FormatException)
        {
            install.Evidence.Add("Executable version could not be read: " + ex.Message);
        }
    }

    private static void ReadAssemblyMarkers(GameInstall install)
    {
        var managed = install.ManagedPath;
        if (managed is null)
        {
            install.Evidence.Add("The Managed folder was not found; format markers are unavailable.");
            return;
        }

        var assembly = Path.Combine(managed, "Assembly-CSharp.dll");
        if (!File.Exists(assembly))
        {
            install.Evidence.Add("Assembly-CSharp.dll was not found; format markers are unavailable.");
            return;
        }

        try
        {
            var info = new FileInfo(assembly);
            if (info.Length > 64L * 1024 * 1024)
            {
                install.Evidence.Add("Assembly-CSharp.dll is unusually large; marker scan skipped.");
                return;
            }

            var bytes = File.ReadAllBytes(assembly);
            var hasFamily = ContainsAscii(bytes, ".family");
            var hasReleaseLabel = ContainsAscii(bytes, "release.label");
            var hasWorld = ContainsAscii(bytes, ".world");

            if (hasFamily)
            {
                install.Evidence.Add($"Assembly-CSharp contains .family (Episode 3 sessions format){(hasReleaseLabel ? " and release.label" : string.Empty)}.");
            }
            else if (hasWorld)
            {
                install.Evidence.Add("Assembly-CSharp contains .world (Episode 1/2 legacy save format).");
            }
            else
            {
                install.Evidence.Add("Assembly-CSharp contains neither .family nor .world; the build may be unsupported.");
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            install.Evidence.Add("Assembly-CSharp.dll could not be read: " + ex.Message);
        }
    }

    private static bool ContainsAscii(byte[] haystack, string needle)
    {
        var pattern = Encoding.ASCII.GetBytes(needle);
        return haystack.AsSpan().IndexOf(pattern) >= 0;
    }

    private static Episode InferEpisodeFromText(string path)
    {
        var lowered = path.ToLowerInvariant();
        if (EpisodeThreeTokens.Any(lowered.Contains) || lowered.Contains("b108"))
        {
            return Episode.Episode3;
        }

        if (EpisodeOneTokens.Any(lowered.Contains))
        {
            return Episode.Episode1;
        }

        if (EpisodeTwoTokens.Any(lowered.Contains))
        {
            return Episode.Episode2;
        }

        return Episode.Unknown;
    }

    private static string? InferBuildFromText(string path)
    {
        var match = BuildToken.Match(Path.GetFileName(path.TrimEnd(Path.DirectorySeparatorChar)));
        if (!match.Success)
        {
            match = BuildToken.Match(path);
        }

        return match.Success ? match.Groups[1].Value : null;
    }

    private static GraphicsApi InferGraphicsApi(string path, out bool guessed)
    {
        var lowered = path.ToLowerInvariant();
        if (lowered.Contains("dx12") || lowered.Contains("d3d12"))
        {
            guessed = false;
            return GraphicsApi.Dx12;
        }

        if (lowered.Contains("dx11") || lowered.Contains("d3d11"))
        {
            guessed = false;
            return GraphicsApi.Dx11;
        }

        guessed = true;
        return GraphicsApi.Unknown;
    }

    private void ApplyOverrides(GameInstall install)
    {
        if (_settings is null)
        {
            return;
        }

        var settings = _settings.Current;
        if (settings.InstallEpisodeOverrides.TryGetValue(install.InstallPath, out var episodeValue)
            && Enum.TryParse<Episode>(episodeValue, ignoreCase: true, out var episode)
            && episode != Episode.Unknown)
        {
            install.Episode = episode;
            install.EpisodeIsGuessed = false;
            install.Evidence.Add($"Episode set to {episode.ToDisplayName()} by the user.");
        }

        if (settings.InstallGraphicsOverrides.TryGetValue(install.InstallPath, out var apiValue)
            && Enum.TryParse<GraphicsApi>(apiValue, ignoreCase: true, out var api)
            && api != GraphicsApi.Unknown)
        {
            install.GraphicsApi = api;
            install.GraphicsApiIsGuessed = false;
            install.Evidence.Add($"Renderer set to {api.ToShortLabel()} by the user.");
        }
    }
}
