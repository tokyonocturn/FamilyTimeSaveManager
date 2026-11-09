using System.Text;
using FamilyTimeSaveManager.Core.Formats;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Services;
using FamilyTimeSaveManager.Core.Storage;

namespace FamilyTimeSaveManager.Core.Tests;

internal static class SaveBytes
{
    private static readonly byte[] MagicEpisode3 = { 87, 83, 67 };
    private static readonly byte[] MagicLegacy = { 87, 83, 68 };

    public static byte[] Episode3(
        IEnumerable<(string Id, byte[] Payload)> sections,
        int? episode = 3,
        int? build = 108,
        bool breakChecksum = false,
        bool forceRle = false,
        bool trailingByte = false)
    {
        var list = sections.ToList();
        if (episode is not null || build is not null)
        {
            var label = new byte[8];
            BitConverter.GetBytes(build ?? 108).CopyTo(label, 0);
            BitConverter.GetBytes(episode ?? 3).CopyTo(label, 4);
            list.Add(("release.label", label));
        }

        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(MagicEpisode3);
            writer.Write(list.Count);

            foreach (var (id, payload) in list)
            {
                var idBytes = Encoding.UTF8.GetBytes(id);
                var stored = forceRle ? RleEncode(payload) : payload;
                var codec = forceRle && stored.Length < payload.Length ? (byte)1 : (byte)0;
                if (codec == 0)
                {
                    stored = payload;
                }

                var crc = Crc32.Compute(payload);
                if (breakChecksum)
                {
                    crc ^= 0x1u;
                }

                writer.Write(idBytes.Length);
                writer.Write(idBytes);
                writer.Write(codec);
                writer.Write(payload.Length);
                writer.Write(stored.Length);
                writer.Write(crc);
                writer.Write(stored);
            }

            if (trailingByte)
            {
                writer.Write((byte)0x42);
            }
        }

        return stream.ToArray();
    }

    public static byte[] Legacy(
        IEnumerable<(string Id, byte[] Payload)> sections,
        bool trailingByte = false)
    {
        var list = sections.ToList();
        using var stream = new MemoryStream();
        using (var writer = new BinaryWriter(stream, Encoding.UTF8, leaveOpen: true))
        {
            writer.Write(MagicLegacy);
            writer.Write(list.Count);

            foreach (var (id, payload) in list)
            {
                var idBytes = Encoding.UTF8.GetBytes(id);
                writer.Write(idBytes.Length);
                writer.Write(idBytes);
                writer.Write(payload.Length);
                writer.Write(payload);
            }

            if (trailingByte)
            {
                writer.Write((byte)0x42);
            }
        }

        return stream.ToArray();
    }

    private static byte[] RleEncode(byte[] input)
    {
        using var output = new MemoryStream();
        var index = 0;
        while (index < input.Length)
        {
            var run = RunLength(input, index);
            if (run >= 3)
            {
                var length = Math.Min(run, 130);
                output.WriteByte((byte)(0x80 | (length - 3)));
                output.WriteByte(input[index]);
                index += length;
                continue;
            }

            var start = index;
            for (index++; index < input.Length && index - start < 128 && RunLength(input, index) < 3; index++)
            {
            }

            var literal = index - start;
            output.WriteByte((byte)(literal - 1));
            output.Write(input, start, literal);
        }

        return output.ToArray();
    }

    private static int RunLength(byte[] data, int start)
    {
        var count = 1;
        while (start + count < data.Length && data[start + count] == data[start] && count < 130)
        {
            count++;
        }

        return count;
    }
}

internal sealed class TestWorkspace : IDisposable
{
    public TestWorkspace(string? existingRoot = null)
    {
        Root = existingRoot ?? Path.Combine(Path.GetTempPath(), "ftsm-test-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);

        Paths = new AppPaths(Path.Combine(Root, "appdata"));
        Log = new ActivityLog(Paths);
        Settings = new SettingsStore(Paths);
        Profiles = new ProfileStore(Paths);
        History = new HistoryStore(Paths);
        Repository = new BackupRepository(Paths, Log);
        Backups = new BackupService(Paths, Repository, History, Log);
        Restores = new RestoreService(Paths, Repository, Backups, History, Log);
        Archives = new ArchiveService(Paths, Repository, Backups, History, Log);
        Retention = new RetentionService();
        Scanner = new SaveScanner(Log);
    }

    public string Root { get; }
    public AppPaths Paths { get; }
    public ActivityLog Log { get; }
    public SettingsStore Settings { get; }
    public ProfileStore Profiles { get; }
    public HistoryStore History { get; }
    public BackupRepository Repository { get; }
    public BackupService Backups { get; }
    public RestoreService Restores { get; }
    public ArchiveService Archives { get; }
    public RetentionService Retention { get; }
    public SaveScanner Scanner { get; }

    public string Write(string relativePath, byte[] content)
    {
        var path = Path.Combine(Root, relativePath.Replace('/', Path.DirectorySeparatorChar));
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        File.WriteAllBytes(path, content);
        return path;
    }

    public SaveFileInfo Describe(string path, Episode episode = Episode.Episode3, IEnumerable<string>? sidecars = null)
    {
        var info = new FileInfo(path);
        var save = new SaveFileInfo
        {
            Path = path,
            Episode = episode,
            Folder = Path.GetDirectoryName(path),
            Length = info.Length,
            CreatedUtc = info.CreationTimeUtc,
            ModifiedUtc = info.LastWriteTimeUtc,
            Sha256 = FileOps.ComputeSha256(path),
            Status = SaveStatus.Valid,
            Metadata = new SaveMetadata
            {
                Format = episode == Episode.Episode3 ? "Episode 3 .family" : "Legacy .world"
            }
        };

        if (sidecars is not null)
        {
            save.SidecarPaths.AddRange(sidecars.Where(File.Exists));
        }

        return save;
    }

    public void Dispose()
    {
        try
        {
            Log.Dispose();
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
