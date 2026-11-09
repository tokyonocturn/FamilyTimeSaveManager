using FamilyTimeSaveManager.Core.Formats;
using FamilyTimeSaveManager.Core.Models;
using Xunit;

namespace FamilyTimeSaveManager.Core.Tests;

public class SaveFormatTests
{
    [Fact]
    public void Episode3_container_is_parsed_with_release_label_and_sections()
    {
        using var workspace = new TestWorkspace();
        var payload = new byte[] { 1, 2, 3, 4, 5 };
        var path = workspace.Write("Sessions/Slot1.family", SaveBytes.Episode3(new[]
        {
            ("session.player", payload),
            ("session.thumbnail.lod0", new byte[] { 9, 9 })
        }));

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveFormatKind.Episode3Family, result.Format);
        Assert.Equal(SaveStatus.Valid, result.Status);
        Assert.Equal(3, result.SectionIds.Count);
        Assert.Contains("session.player", result.SectionIds);
        Assert.Equal(108, result.ReleaseBuild);
        Assert.Equal(3, result.ReleaseEpisode);
        Assert.True(result.HasThumbnail);
        Assert.True(result.ChecksumsVerified);
        Assert.Equal(0, result.ChecksumFailures);
    }

    [Fact]
    public void Episode3_checksum_mismatch_is_reported_as_corrupt()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.Write("Sessions/Bad.family", SaveBytes.Episode3(
            new[] { ("session.player", new byte[] { 1, 2, 3 }) },
            breakChecksum: true));

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveStatus.Corrupt, result.Status);
        Assert.True(result.ChecksumFailures >= 1);
        Assert.Contains("checksum", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Episode3_rle_sections_are_decompressed_and_verified()
    {
        using var workspace = new TestWorkspace();
        var repetitive = Enumerable.Repeat((byte)0x41, 500).ToArray();
        var path = workspace.Write("Sessions/Compressed.family", SaveBytes.Episode3(
            new[] { ("session.big", repetitive) },
            forceRle: true));

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveStatus.Valid, result.Status);
        Assert.Equal(1, result.ChecksumFailures == 0 ? 1 : 0);
        Assert.Equal(0, result.ChecksumFailures);
    }

    [Fact]
    public void Episode3_truncated_payload_is_reported_as_corrupt()
    {
        using var workspace = new TestWorkspace();
        var full = SaveBytes.Episode3(new[] { ("session.player", Enumerable.Repeat((byte)7, 200).ToArray()) });
        var truncated = full.Take(full.Length - 50).ToArray();
        var path = workspace.Write("Sessions/Truncated.family", truncated);

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveStatus.Corrupt, result.Status);
        Assert.Contains("cut short", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Episode3_trailing_data_is_reported_as_corrupt()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.Write("Sessions/Trailing.family", SaveBytes.Episode3(
            new[] { ("session.player", new byte[] { 5, 5, 5 }) },
            trailingByte: true));

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveStatus.Corrupt, result.Status);
        Assert.Contains("trailing", result.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Legacy_world_container_is_parsed_without_checksum_claims()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.Write("Saves/World.world", SaveBytes.Legacy(new[]
        {
            ("world.header", new byte[] { 1 }),
            ("world.chunks", new byte[] { 2, 3, 4 })
        }));

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveFormatKind.LegacyWorld, result.Format);
        Assert.Equal(SaveStatus.Valid, result.Status);
        Assert.False(result.ChecksumsVerified);
        Assert.Equal(2, result.SectionIds.Count);
    }

    [Fact]
    public void Unknown_header_is_reported_as_unsupported()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.Write("Sessions/Foreign.family", new byte[] { 0x50, 0x4B, 0x03, 0x04, 0x00 });

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveStatus.Unsupported, result.Status);
    }

    [Fact]
    public void Empty_file_is_reported_as_empty()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.Write("Sessions/Empty.family", Array.Empty<byte>());

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveStatus.Empty, result.Status);
        Assert.Equal(0, result.Length);
    }

    [Fact]
    public void Impossible_section_count_is_reported_as_corrupt()
    {
        using var workspace = new TestWorkspace();
        var bytes = new byte[] { 87, 83, 67, 0xFF, 0xFF, 0x00, 0x00 };
        var path = workspace.Write("Sessions/BadCount.family", bytes);

        var result = FamilySaveFormat.Parse(path);

        Assert.Equal(SaveStatus.Corrupt, result.Status);
        Assert.Contains("section count", result.Message, StringComparison.OrdinalIgnoreCase);
    }
}
