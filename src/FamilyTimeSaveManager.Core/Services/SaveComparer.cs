using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.Core.Services;

public sealed record SaveComparison(
    SaveFileInfo Left,
    SaveFileInfo Right,
    bool Identical,
    string Summary,
    IReadOnlyList<string> Differences,
    IReadOnlyList<string> SharedSections);

/// <summary>
/// File-level and metadata comparison only. Save payloads are never decoded into game state and
/// no compatibility is inferred from a matching header.
/// </summary>
public static class SaveComparer
{
    public static SaveComparison Compare(SaveFileInfo left, SaveFileInfo right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var differences = new List<string>();
        var identical = left.Sha256 is not null
                        && right.Sha256 is not null
                        && string.Equals(left.Sha256, right.Sha256, StringComparison.OrdinalIgnoreCase);

        if (identical)
        {
            differences.Add("The files are byte-for-byte identical (same SHA-256).");
        }
        else
        {
            differences.Add($"Different content. SHA-256 {Short(left.Sha256)} vs {Short(right.Sha256)}.");
        }

        if (left.Length != right.Length)
        {
            differences.Add($"Size differs: {left.SizeDisplay} vs {right.SizeDisplay}.");
        }

        if (left.Episode != right.Episode)
        {
            differences.Add($"Episode: {left.Episode.ToDisplayName()} vs {right.Episode.ToDisplayName()}.");
        }

        if (left.Metadata.Format != right.Metadata.Format)
        {
            differences.Add($"Container format: {left.Metadata.FormatDisplay} vs {right.Metadata.FormatDisplay}.");
        }

        if (left.Metadata.ReleaseBuild != right.Metadata.ReleaseBuild)
        {
            differences.Add($"Embedded build: {Display(left.Metadata.ReleaseBuild)} vs {Display(right.Metadata.ReleaseBuild)}.");
        }

        if (left.Metadata.ReleaseEpisode != right.Metadata.ReleaseEpisode)
        {
            differences.Add($"Embedded episode: {Display(left.Metadata.ReleaseEpisode)} vs {Display(right.Metadata.ReleaseEpisode)}.");
        }

        if (left.Metadata.SectionCount != right.Metadata.SectionCount)
        {
            differences.Add($"Section count: {left.Metadata.SectionCount} vs {right.Metadata.SectionCount}.");
        }

        if (left.Status != right.Status)
        {
            differences.Add($"Integrity status: {left.StatusDisplay} vs {right.StatusDisplay}.");
        }

        var leftSections = new HashSet<string>(left.Metadata.SectionIds, StringComparer.Ordinal);
        var rightSections = new HashSet<string>(right.Metadata.SectionIds, StringComparer.Ordinal);
        var shared = leftSections.Intersect(rightSections, StringComparer.Ordinal).OrderBy(s => s, StringComparer.Ordinal).ToList();

        foreach (var section in leftSections.Except(rightSections).OrderBy(s => s, StringComparer.Ordinal))
        {
            differences.Add($"Only in the left save: '{section}'.");
        }

        foreach (var section in rightSections.Except(leftSections).OrderBy(s => s, StringComparer.Ordinal))
        {
            differences.Add($"Only in the right save: '{section}'.");
        }

        foreach (var section in shared)
        {
            if (left.Metadata.SectionLengths.TryGetValue(section, out var leftLength)
                && right.Metadata.SectionLengths.TryGetValue(section, out var rightLength)
                && leftLength != rightLength)
            {
                differences.Add($"Section '{section}' payload differs: {leftLength} vs {rightLength} bytes.");
            }
        }

        if (left.Metadata.ChecksumFailures > 0 || right.Metadata.ChecksumFailures > 0)
        {
            differences.Add("At least one save failed its embedded checksum, so some section comparisons were skipped.");
        }

        var summary = identical
            ? "The two saves are identical."
            : left.Metadata.Format == right.Metadata.Format
                ? "Same container format, different content. Section-level payloads are not decoded, so gameplay state cannot be compared."
                : "Different container formats. These saves come from different game versions and no compatibility is implied.";

        return new SaveComparison(left, right, identical, summary, differences, shared);
    }

    private static string Short(string? hash) =>
        string.IsNullOrWhiteSpace(hash) ? "(unavailable)" : hash[..Math.Min(12, hash.Length)] + "…";

    private static string Display(int? value) => value?.ToString() ?? "not embedded";
}
