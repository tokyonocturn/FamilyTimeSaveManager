using System.Text;
using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.Core.Formats;

public enum SaveFormatKind
{
    Unknown = 0,
    Episode3Family = 1,
    LegacyWorld = 2
}

public sealed class SaveParseResult
{
    public SaveFormatKind Format { get; init; } = SaveFormatKind.Unknown;
    public SaveStatus Status { get; init; } = SaveStatus.Unknown;
    public string? Message { get; init; }
    public long Length { get; init; }
    public List<string> SectionIds { get; init; } = new();
    public Dictionary<string, int> SectionLengths { get; init; } = new(StringComparer.Ordinal);
    public int? ReleaseEpisode { get; init; }
    public int? ReleaseBuild { get; init; }
    public bool HasThumbnail { get; init; }
    public bool ChecksumsVerified { get; init; }
    public int ChecksumFailures { get; init; }
}

/// <summary>
/// Read-only parser for the two world-save containers shipped by Family Time.
///
/// Episode 3 (.family): bytes 'W','S','C', int32 section count, then for each section
/// int32 id length, UTF-8 id, byte codec (0 raw, 1 RLE), int32 raw length, int32 stored
/// length, uint32 CRC-32 of the raw bytes, stored bytes. A "release.label" section carries
/// the build and episode numbers as two little-endian int32 values.
///
/// Episodes 1 and 2 (.world): bytes 'W','S','D', int32 section count, then for each section
/// int32 id length, UTF-8 id, int32 payload length, payload. No checksums are embedded.
///
/// The parser never writes to the file and never guesses at a format it does not recognise.
/// </summary>
public static class FamilySaveFormat
{
    public const string Episode3Extension = ".family";
    public const string LegacyExtension = ".world";
    public const int MaxSections = 1024;
    public const int MaxSectionIdBytes = 256;
    public const int MaxSectionBytes = 512 * 1024 * 1024;

    private static readonly byte[] MagicEpisode3 = { 87, 83, 67 };
    private static readonly byte[] MagicLegacy = { 87, 83, 68 };

    public static SaveParseResult Parse(string path, bool verifyChecksums = true)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            return new SaveParseResult { Status = SaveStatus.Unreadable, Message = "No file was given." };
        }

        long length;
        try
        {
            length = new FileInfo(path).Length;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            return new SaveParseResult { Status = SaveStatus.Unreadable, Message = ex.Message };
        }

        if (length == 0)
        {
            return new SaveParseResult { Status = SaveStatus.Empty, Message = "The file is empty.", Length = 0 };
        }

        try
        {
            using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 1 << 16, FileOptions.SequentialScan);
            var magic = new byte[3];
            if (!TryReadExactly(stream, magic, 3))
            {
                return new SaveParseResult { Status = SaveStatus.Corrupt, Message = "The file is too short to contain a save header.", Length = length };
            }

            if (magic.SequenceEqual(MagicEpisode3))
            {
                return ParseEpisode3(stream, length, verifyChecksums);
            }

            if (magic.SequenceEqual(MagicLegacy))
            {
                return ParseLegacy(stream, length);
            }

            return new SaveParseResult
            {
                Status = SaveStatus.Unsupported,
                Message = "The file does not start with a recognised Family Time save header.",
                Length = length
            };
        }
        catch (EndOfStreamException)
        {
            return new SaveParseResult { Status = SaveStatus.Corrupt, Message = "The save file ends before its section table is complete.", Length = length };
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new SaveParseResult { Status = SaveStatus.Unreadable, Message = ex.Message, Length = length };
        }
    }

    private static SaveParseResult ParseEpisode3(Stream stream, long length, bool verifyChecksums)
    {
        var result = new SaveParseResult { Format = SaveFormatKind.Episode3Family, Length = length };
        var sectionIds = new List<string>();
        var sectionLengths = new Dictionary<string, int>(StringComparer.Ordinal);
        int? releaseEpisode = null;
        int? releaseBuild = null;
        var thumbnail = false;
        var checksumFailures = 0;
        var notes = new List<string>();

        if (!TryReadInt32(stream, out var sectionCount) || sectionCount < 0 || sectionCount > MaxSections)
        {
            return new SaveParseResult
            {
                Format = SaveFormatKind.Episode3Family,
                Status = SaveStatus.Corrupt,
                Message = "The section count in the header is not valid.",
                Length = length
            };
        }

        for (var index = 0; index < sectionCount; index++)
        {
            if (!TryReadInt32(stream, out var idLength) || idLength <= 0 || idLength > MaxSectionIdBytes || idLength > Remaining(stream))
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.Episode3Family,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section {index + 1} has an invalid identifier length.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            var idBytes = new byte[idLength];
            if (!TryReadExactly(stream, idBytes, idLength))
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.Episode3Family,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section {index + 1} ends inside its identifier.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            var id = Encoding.UTF8.GetString(idBytes);

            if (!TryReadByte(stream, out var codec) || !TryReadInt32(stream, out var rawLength)
                || !TryReadInt32(stream, out var storedLength) || !TryReadUInt32(stream, out var expectedCrc))
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.Episode3Family,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section '{id}' has an incomplete header.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            if (rawLength < 0 || rawLength > MaxSectionBytes || storedLength < 0 || storedLength > MaxSectionBytes)
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.Episode3Family,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section '{id}' declares an impossible size.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            if (storedLength > Remaining(stream))
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.Episode3Family,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section '{id}' is cut short: the file ends before its payload.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            if (codec > 1)
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.Episode3Family,
                    Status = SaveStatus.Unsupported,
                    Message = $"Section '{id}' uses an unknown compression codec ({codec}).",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            var isReleaseLabel = string.Equals(id, "release.label", StringComparison.Ordinal);
            var mustRead = verifyChecksums || isReleaseLabel;
            if (mustRead)
            {
                var stored = new byte[storedLength];
                if (!TryReadExactly(stream, stored, storedLength))
                {
                    return new SaveParseResult
                    {
                        Format = SaveFormatKind.Episode3Family,
                        Status = SaveStatus.Corrupt,
                        Message = $"Section '{id}' could not be read.",
                        Length = length,
                        SectionIds = sectionIds,
                        SectionLengths = sectionLengths
                    };
                }

                byte[] raw;
                try
                {
                    raw = codec == 0 ? stored : DecodeRle(stored, rawLength);
                }
                catch (InvalidDataException ex)
                {
                    return new SaveParseResult
                    {
                        Format = SaveFormatKind.Episode3Family,
                        Status = SaveStatus.Corrupt,
                        Message = $"Section '{id}' could not be decompressed: {ex.Message}",
                        Length = length,
                        SectionIds = sectionIds,
                        SectionLengths = sectionLengths
                    };
                }

                if (raw.Length != rawLength)
                {
                    return new SaveParseResult
                    {
                        Format = SaveFormatKind.Episode3Family,
                        Status = SaveStatus.Corrupt,
                        Message = $"Section '{id}' decompressed to {raw.Length} bytes but declared {rawLength}.",
                        Length = length,
                        SectionIds = sectionIds,
                        SectionLengths = sectionLengths
                    };
                }

                if (verifyChecksums && Crc32.Compute(raw) != expectedCrc)
                {
                    checksumFailures++;
                }

                if (isReleaseLabel && rawLength >= 8)
                {
                    var build = BitConverter.ToInt32(raw, 0);
                    var episode = BitConverter.ToInt32(raw, 4);
                    if (build >= 0)
                    {
                        releaseBuild = build;
                    }

                    if (episode is >= 1 and <= 3)
                    {
                        releaseEpisode = episode;
                    }
                }
            }
            else
            {
                stream.Seek(storedLength, SeekOrigin.Current);
            }

            sectionIds.Add(id);
            sectionLengths[id] = rawLength;
            if (id.StartsWith("session.thumbnail", StringComparison.Ordinal))
            {
                thumbnail = true;
            }
        }

        if (stream.Position != stream.Length)
        {
            notes.Add($"{stream.Length - stream.Position} trailing byte(s) after the last section.");
        }

        if (checksumFailures > 0)
        {
            notes.Insert(0, $"{checksumFailures} section checksum(s) failed.");
        }

        var status = notes.Count == 0 ? SaveStatus.Valid : checksumFailures > 0 || stream.Position != stream.Length ? SaveStatus.Corrupt : SaveStatus.Suspicious;

        return new SaveParseResult
        {
            Format = SaveFormatKind.Episode3Family,
            Status = status,
            Message = notes.Count == 0 ? "Episode 3 container with per-section CRC-32 checksums." : string.Join(" ", notes),
            Length = length,
            SectionIds = sectionIds,
            SectionLengths = sectionLengths,
            ReleaseEpisode = releaseEpisode,
            ReleaseBuild = releaseBuild,
            HasThumbnail = thumbnail,
            ChecksumsVerified = verifyChecksums,
            ChecksumFailures = checksumFailures
        };
    }

    private static SaveParseResult ParseLegacy(Stream stream, long length)
    {
        var sectionIds = new List<string>();
        var sectionLengths = new Dictionary<string, int>(StringComparer.Ordinal);
        int? releaseEpisode = null;
        int? releaseBuild = null;

        if (!TryReadInt32(stream, out var sectionCount) || sectionCount < 0 || sectionCount > MaxSections)
        {
            return new SaveParseResult
            {
                Format = SaveFormatKind.LegacyWorld,
                Status = SaveStatus.Corrupt,
                Message = "The section count in the header is not valid.",
                Length = length
            };
        }

        for (var index = 0; index < sectionCount; index++)
        {
            if (!TryReadInt32(stream, out var idLength) || idLength <= 0 || idLength > MaxSectionIdBytes || idLength > Remaining(stream))
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.LegacyWorld,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section {index + 1} has an invalid identifier length.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            var idBytes = new byte[idLength];
            if (!TryReadExactly(stream, idBytes, idLength))
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.LegacyWorld,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section {index + 1} ends inside its identifier.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            var id = Encoding.UTF8.GetString(idBytes);

            if (!TryReadInt32(stream, out var payloadLength) || payloadLength < 0 || payloadLength > MaxSectionBytes)
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.LegacyWorld,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section '{id}' declares an impossible size.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            if (payloadLength > Remaining(stream))
            {
                return new SaveParseResult
                {
                    Format = SaveFormatKind.LegacyWorld,
                    Status = SaveStatus.Corrupt,
                    Message = $"Section '{id}' is cut short: the file ends before its payload.",
                    Length = length,
                    SectionIds = sectionIds,
                    SectionLengths = sectionLengths
                };
            }

            if (string.Equals(id, "release.label", StringComparison.Ordinal) && payloadLength >= 8)
            {
                var payload = new byte[8];
                if (TryReadExactly(stream, payload, 8))
                {
                    releaseBuild = BitConverter.ToInt32(payload, 0);
                    var episode = BitConverter.ToInt32(payload, 4);
                    releaseEpisode = episode is >= 1 and <= 3 ? episode : null;
                    stream.Seek(payloadLength - 8, SeekOrigin.Current);
                }
            }
            else
            {
                stream.Seek(payloadLength, SeekOrigin.Current);
            }

            sectionIds.Add(id);
            sectionLengths[id] = payloadLength;
        }

        if (stream.Position != stream.Length)
        {
            return new SaveParseResult
            {
                Format = SaveFormatKind.LegacyWorld,
                Status = SaveStatus.Corrupt,
                Message = $"{stream.Length - stream.Position} trailing byte(s) after the last section.",
                Length = length,
                SectionIds = sectionIds,
                SectionLengths = sectionLengths,
                ReleaseEpisode = releaseEpisode,
                ReleaseBuild = releaseBuild
            };
        }

        return new SaveParseResult
        {
            Format = SaveFormatKind.LegacyWorld,
            Status = SaveStatus.Valid,
            Message = "Legacy .world container. This format has no embedded checksums, so only structure can be validated.",
            Length = length,
            SectionIds = sectionIds,
            SectionLengths = sectionLengths,
            ReleaseEpisode = releaseEpisode,
            ReleaseBuild = releaseBuild,
            ChecksumsVerified = false
        };
    }

    /// <summary>
    /// Decoder for the Episode 3 run-length encoding. A control byte with 0x80 set introduces a
    /// run of (value &amp; 0x7F) + 3 copies of the next byte; otherwise it introduces
    /// (value &amp; 0x7F) + 1 literal bytes.
    /// </summary>
    internal static byte[] DecodeRle(ReadOnlySpan<byte> stored, int expectedLength)
    {
        if (expectedLength == 0)
        {
            return Array.Empty<byte>();
        }

        var output = new byte[expectedLength];
        var written = 0;
        var index = 0;

        while (index < stored.Length && written < expectedLength)
        {
            var control = stored[index++];
            if ((control & 0x80) != 0)
            {
                var runLength = (control & 0x7F) + 3;
                if (index >= stored.Length)
                {
                    throw new InvalidDataException("run marker without a value byte");
                }

                var value = stored[index++];
                for (var i = 0; i < runLength && written < expectedLength; i++)
                {
                    output[written++] = value;
                }
            }
            else
            {
                var literalLength = (control & 0x7F) + 1;
                if (index + literalLength > stored.Length)
                {
                    throw new InvalidDataException("literal run longer than the stored payload");
                }

                var take = Math.Min(literalLength, expectedLength - written);
                stored.Slice(index, take).CopyTo(output.AsSpan(written));
                written += take;
                index += literalLength;
            }
        }

        if (written != expectedLength)
        {
            throw new InvalidDataException($"decoded {written} of {expectedLength} bytes");
        }

        return output;
    }

    private static long Remaining(Stream stream) => Math.Max(0, stream.Length - stream.Position);

    private static bool TryReadExactly(Stream stream, byte[] buffer, int count)
    {
        var offset = 0;
        while (offset < count)
        {
            var read = stream.Read(buffer, offset, count - offset);
            if (read == 0)
            {
                return false;
            }

            offset += read;
        }

        return true;
    }

    private static bool TryReadByte(Stream stream, out byte value)
    {
        var single = new byte[1];
        if (TryReadExactly(stream, single, 1))
        {
            value = single[0];
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadInt32(Stream stream, out int value)
    {
        var buffer = new byte[4];
        if (TryReadExactly(stream, buffer, 4))
        {
            value = BitConverter.ToInt32(buffer, 0);
            return true;
        }

        value = 0;
        return false;
    }

    private static bool TryReadUInt32(Stream stream, out uint value)
    {
        var buffer = new byte[4];
        if (TryReadExactly(stream, buffer, 4))
        {
            value = BitConverter.ToUInt32(buffer, 0);
            return true;
        }

        value = 0;
        return false;
    }
}
