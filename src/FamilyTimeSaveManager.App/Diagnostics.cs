using System.Text.Json;
using FamilyTimeSaveManager.Core;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.App;

/// <summary>
/// Headless mode used for verification: prints detected installs, save locations and parsed save
/// metadata as JSON without opening a window.
/// </summary>
internal static class Diagnostics
{
    public static int Run(string[] args)
    {
        var outputPath = args.Length > 0
            ? Path.GetFullPath(args[0])
            : Path.Combine(Environment.CurrentDirectory, "ftsm-diagnostics.json");

        try
        {
            using var services = AppServices.Create();
            var installs = services.DetectInstalls();
            var locations = services.ResolveLocations(installs);
            var saves = services.Scanner.Scan(locations, verifyChecksums: true);
            var backups = services.Backups.List();

            var payload = new
            {
                application = Program.ApplicationName,
                version = typeof(Program).Assembly.GetName().Version?.ToString() ?? "unknown",
                generatedUtc = DateTime.UtcNow,
                dataRoot = services.Paths.DataRoot,
                installs = installs.Select(i => new
                {
                    path = i.InstallPath,
                    episode = i.Episode.ToString(),
                    renderer = i.GraphicsApi.ToString(),
                    company = i.CompanyName,
                    product = i.ProductName,
                    unity = i.UnityVersion,
                    build = i.BuildLabel,
                    sessionsFolder = i.SessionsFolder,
                    evidence = i.Evidence
                }),
                locations = locations.Select(l => new
                {
                    path = l.Path,
                    episode = l.Episode.ToString(),
                    source = l.Source,
                    exists = l.Exists,
                    note = l.Note,
                    install = l.InstallPath
                }),
                saves = saves.Select(s => new
                {
                    file = s.FileName,
                    path = s.Path,
                    episode = s.Episode.ToString(),
                    size = s.Length,
                    modifiedUtc = s.ModifiedUtc,
                    status = s.Status.ToString(),
                    statusMessage = s.StatusMessage,
                    sha256 = s.Sha256,
                    format = s.Metadata.Format,
                    sections = s.Metadata.SectionCount,
                    releaseEpisode = s.Metadata.ReleaseEpisode,
                    releaseBuild = s.Metadata.ReleaseBuild,
                    checksumsVerified = s.Metadata.ChecksumsVerified,
                    sidecars = s.SidecarPaths
                }),
                backups = backups.Select(b => new
                {
                    id = b.Id,
                    name = b.Name,
                    kind = b.Kind.ToString(),
                    episode = b.Episode.ToString(),
                    createdUtc = b.CreatedUtc,
                    bytes = b.TotalBytes,
                    verified = b.Verified,
                    files = b.Files.Select(f => new { f.StoredName, f.Sha256, f.Length, f.Role })
                })
            };

            var json = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonFile.Options) { WriteIndented = true });
            var directory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(outputPath, json);
            Console.WriteLine(outputPath);
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(outputPath + ".error.txt", ex.ToString());
            }
            catch (IOException)
            {
            }

            Console.Error.WriteLine(ex.Message);
            return 1;
        }
    }
}
