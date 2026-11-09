using System.Security.Cryptography;
using System.Text;

namespace FamilyTimeSaveManager.Core.Storage;

public static class FileOps
{
    private static readonly string[] ReservedNames =
    {
        "CON", "PRN", "AUX", "NUL", "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9"
    };

    public static string ComputeSha256(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, FileOptions.SequentialScan);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream)).ToLowerInvariant();
    }

    public static string ComputeSha256(byte[] data)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(data)).ToLowerInvariant();
    }

    /// <summary>
    /// True when <paramref name="candidatePath"/> resolves to something inside
    /// <paramref name="rootDirectory"/>. Used to reject archive entries that escape the target folder.
    /// </summary>
    public static bool IsWithin(string rootDirectory, string candidatePath)
    {
        if (string.IsNullOrWhiteSpace(rootDirectory) || string.IsNullOrWhiteSpace(candidatePath))
        {
            return false;
        }

        var root = Path.GetFullPath(rootDirectory);
        var candidate = Path.GetFullPath(candidatePath);
        var rootWithSeparator = root.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                                + Path.DirectorySeparatorChar;

        return candidate.Equals(root, StringComparison.OrdinalIgnoreCase)
               || candidate.StartsWith(rootWithSeparator, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Turns arbitrary user or archive text into a single safe file name. Never returns a path
    /// containing a separator, a parent reference, a reserved device name or trailing dot/space.
    /// </summary>
    public static string SanitizeFileName(string? name, string fallback = "file")
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return fallback;
        }

        var invalid = Path.GetInvalidFileNameChars();
        var builder = new StringBuilder(name.Length);
        foreach (var ch in name.Trim())
        {
            builder.Append(invalid.Contains(ch) || ch == '/' || ch == '\\' ? '_' : ch);
        }

        var cleaned = builder.ToString().Trim().TrimEnd('.', ' ');
        if (cleaned.Length == 0 || cleaned == "." || cleaned == "..")
        {
            return fallback;
        }

        var stem = Path.GetFileNameWithoutExtension(cleaned);
        if (ReservedNames.Contains(stem, StringComparer.OrdinalIgnoreCase))
        {
            cleaned = "_" + cleaned;
        }

        if (cleaned.Length > 120)
        {
            var extension = Path.GetExtension(cleaned);
            if (extension.Length > 16)
            {
                extension = string.Empty;
            }

            var maxStem = Math.Max(1, 120 - extension.Length);
            cleaned = cleaned[..Math.Min(maxStem, cleaned.Length)] + extension;
        }

        return cleaned;
    }

    public static void CopyFileAtomic(string sourcePath, string destinationPath)
    {
        var directory = Path.GetDirectoryName(destinationPath)
                        ?? throw new ArgumentException("Destination needs a folder.", nameof(destinationPath));
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(destinationPath)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var source = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, FileOptions.SequentialScan))
            using (var destination = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.WriteThrough))
            {
                source.CopyTo(destination);
                destination.Flush(flushToDisk: true);
            }

            Publish(tempPath, destinationPath);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public static void WriteAllBytesAtomic(string path, byte[] data)
    {
        var directory = Path.GetDirectoryName(path)
                        ?? throw new ArgumentException("Path needs a folder.", nameof(path));
        Directory.CreateDirectory(directory);

        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        try
        {
            using (var stream = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1 << 16, FileOptions.WriteThrough))
            {
                stream.Write(data);
                stream.Flush(flushToDisk: true);
            }

            Publish(tempPath, path);
        }
        finally
        {
            TryDelete(tempPath);
        }
    }

    public static void WriteAllTextAtomic(string path, string text) =>
        WriteAllBytesAtomic(path, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(text));

    public static void TryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void Publish(string tempPath, string destinationPath)
    {
        if (File.Exists(destinationPath))
        {
            File.Replace(tempPath, destinationPath, destinationBackupFileName: null, ignoreMetadataErrors: true);
        }
        else
        {
            File.Move(tempPath, destinationPath);
        }
    }
}
