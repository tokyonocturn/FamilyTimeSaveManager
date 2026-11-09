using FamilyTimeSaveManager.Core.Models;

namespace FamilyTimeSaveManager.Core.Services;

/// <summary>
/// Retention applies only to scheduled backups. Manual, pre-restore and imported backups are
/// never removed automatically.
/// </summary>
public sealed class RetentionService
{
    public IReadOnlyList<BackupRecord> SelectExpired(IEnumerable<BackupRecord> backups, AppSettings settings)
    {
        ArgumentNullException.ThrowIfNull(backups);
        ArgumentNullException.ThrowIfNull(settings);

        var automatic = backups
            .Where(b => b.Kind == BackupKind.Automatic)
            .GroupBy(b => string.IsNullOrWhiteSpace(b.ProfileId) ? Profile.DefaultId : b.ProfileId, StringComparer.Ordinal);

        var expired = new Dictionary<string, BackupRecord>(StringComparer.Ordinal);

        foreach (var group in automatic)
        {
            var ordered = group.OrderByDescending(b => b.CreatedUtc).ToList();

            for (var index = settings.RetentionAutoBackups; index < ordered.Count; index++)
            {
                expired[ordered[index].Id] = ordered[index];
            }

            if (settings.RetentionDays > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-settings.RetentionDays);
                foreach (var record in ordered.Where(b => b.CreatedUtc < cutoff))
                {
                    expired[record.Id] = record;
                }
            }
        }

        return expired.Values
            .OrderBy(b => b.CreatedUtc)
            .ToList();
    }
}
