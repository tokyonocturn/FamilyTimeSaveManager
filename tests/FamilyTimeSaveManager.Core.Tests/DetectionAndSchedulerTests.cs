using System.Text;
using FamilyTimeSaveManager.Core.Detection;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Services;
using FamilyTimeSaveManager.Core.Storage;
using Xunit;

namespace FamilyTimeSaveManager.Core.Tests;

public class DetectionAndSchedulerTests
{
    [Fact]
    public void Ep3_install_is_identified_from_the_assembly_marker()
    {
        using var workspace = new TestWorkspace();
        var install = Path.Combine(workspace.Root, "FamilyTime_EP3_Test");
        Directory.CreateDirectory(Path.Combine(install, "FamilyTime_Data", "Managed"));
        File.WriteAllBytes(Path.Combine(install, "FamilyTime.exe"), Array.Empty<byte>());
        File.WriteAllText(Path.Combine(install, "FamilyTime_Data", "app.info"), "sgthale\r\nFamily Time\r\n");
        File.WriteAllBytes(
            Path.Combine(install, "FamilyTime_Data", "Managed", "Assembly-CSharp.dll"),
            Encoding.ASCII.GetBytes("nonsense release.label .family nonsense"));

        var detector = new GameInstallDetector(workspace.Log);
        var result = detector.Inspect(install);

        Assert.NotNull(result);
        Assert.Equal(Episode.Episode3, result!.Episode);
        Assert.Equal("sgthale", result.CompanyName);
        Assert.Equal("Family Time", result.ProductName);
        Assert.Equal(Path.Combine(install, "FamilyTime_Data", "Sessions"), result.SessionsFolder);
        Assert.Contains(result.Evidence, e => e.Contains(".family", StringComparison.Ordinal));
    }

    [Fact]
    public void Legacy_install_uses_the_shared_persistent_folder()
    {
        using var workspace = new TestWorkspace();
        var install = Path.Combine(workspace.Root, "Episode 1", "FamilyTime_Patreon");
        Directory.CreateDirectory(Path.Combine(install, "FamilyTime_Data", "Managed"));
        File.WriteAllBytes(Path.Combine(install, "FamilyTime.exe"), Array.Empty<byte>());
        File.WriteAllText(Path.Combine(install, "FamilyTime_Data", "app.info"), "sgthale\r\nFamily Time\r\n");
        File.WriteAllBytes(
            Path.Combine(install, "FamilyTime_Data", "Managed", "Assembly-CSharp.dll"),
            Encoding.ASCII.GetBytes("legacy .world format"));

        var detector = new GameInstallDetector(workspace.Log);
        var result = detector.Inspect(install);
        Assert.NotNull(result);
        Assert.Equal(Episode.Episode1, result!.Episode);
        Assert.True(result.EpisodeIsGuessed);
        Assert.Null(result.SessionsFolder);

        var locations = new SaveLocationService(workspace.Settings).Resolve(new[] { result });
        var legacy = Assert.Single(locations.Where(l => l.Episode == Episode.Episode1));
        Assert.Contains("LocalLow", legacy.Path, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Family Time", legacy.Path, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Scanner_reports_status_episode_and_sidecar_without_writing()
    {
        using var workspace = new TestWorkspace();
        var saves = Path.Combine(workspace.Root, "Sessions");
        Directory.CreateDirectory(saves);
        var savePath = Path.Combine(saves, "Slot1.family");
        File.WriteAllBytes(savePath, SaveBytes.Episode3(new[] { ("session.player", new byte[] { 1, 2, 3 }) }));
        var sidecarFolder = Path.Combine(workspace.Root, "LocalLow");
        Directory.CreateDirectory(sidecarFolder);
        File.WriteAllBytes(Path.Combine(sidecarFolder, "Slot1.bpm"), new byte[] { 4, 4 });

        var before = File.ReadAllBytes(savePath);
        var location = new SaveLocation(
            saves,
            Episode.Episode3,
            "test",
            SidecarFolder: sidecarFolder,
            BuildLabel: "108");

        var found = workspace.Scanner.Scan(new[] { location });

        Assert.Single(found);
        var save = found[0];
        Assert.Equal(SaveStatus.Valid, save.Status);
        Assert.Equal(Episode.Episode3, save.Episode);
        Assert.Equal(108, save.Metadata.ReleaseBuild);
        Assert.Single(save.SidecarPaths);
        Assert.True(save.Metadata.HasBuildParts);
        Assert.False(string.IsNullOrEmpty(save.Sha256));
        Assert.Equal(before, File.ReadAllBytes(savePath));
    }

    [Fact]
    public void Scanner_flags_an_episode_label_that_contradicts_the_folder()
    {
        using var workspace = new TestWorkspace();
        var saves = Path.Combine(workspace.Root, "Sessions");
        Directory.CreateDirectory(saves);
        File.WriteAllBytes(
            Path.Combine(saves, "Slot1.family"),
            SaveBytes.Episode3(new[] { ("session.player", new byte[] { 1 }) }, episode: 2));

        var location = new SaveLocation(saves, Episode.Episode3, "test");
        var save = Assert.Single(workspace.Scanner.Scan(new[] { location }));

        Assert.Equal(SaveStatus.Suspicious, save.Status);
        Assert.Contains("labels itself as Episode 2", save.StatusMessage, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compare_reports_identical_files_and_format_differences()
    {
        using var workspace = new TestWorkspace();
        var first = workspace.Write("Sessions/A.family", SaveBytes.Episode3(new[] { ("session.player", new byte[] { 1, 2 }) }));
        var second = workspace.Write("Sessions/B.family", SaveBytes.Episode3(new[] { ("session.player", new byte[] { 1, 2 }) }));
        var different = workspace.Write("Sessions/C.family", SaveBytes.Episode3(new[] { ("session.player", new byte[] { 1, 2, 3 }) }));

        var identical = SaveComparer.Compare(workspace.Describe(first), workspace.Describe(second));
        Assert.True(identical.Identical);
        Assert.Contains("identical", identical.Summary, StringComparison.OrdinalIgnoreCase);

        var changed = SaveComparer.Compare(workspace.Describe(first), workspace.Describe(different));
        Assert.False(changed.Identical);
        Assert.Contains(changed.Differences, d => d.Contains("Size differs", StringComparison.Ordinal));
    }

    [Fact]
    public void Scheduled_backup_only_runs_when_the_save_changes()
    {
        using var workspace = new TestWorkspace();
        var savePath = workspace.Write("Sessions/Slot1.family", SaveBytes.Episode3(new[] { ("session.player", new byte[] { 1 }) }));
        var providerCalls = 0;
        var saves = new List<SaveFileInfo>();

        var service = new AutoBackupService(
            _ =>
            {
                providerCalls++;
                return saves;
            },
            workspace.Backups,
            workspace.Repository,
            workspace.Settings,
            workspace.Retention,
            workspace.History,
            workspace.Log);

        saves.Add(workspace.Describe(savePath));
        Assert.Equal(1, service.RunOnce(manual: true));
        Assert.Equal(0, service.RunOnce(manual: true));

        File.WriteAllBytes(savePath, SaveBytes.Episode3(new[] { ("session.player", new byte[] { 1, 2 }) }));
        saves.Clear();
        saves.Add(workspace.Describe(savePath));

        Assert.Equal(1, service.RunOnce(manual: true));
        Assert.Equal(2, workspace.Repository.List().Count);
        Assert.True(providerCalls >= 3);
        service.Dispose();
    }

    [Fact]
    public void Retention_removes_only_old_scheduled_backups()
    {
        using var workspace = new TestWorkspace();
        var now = DateTime.UtcNow;
        var records = new List<BackupRecord>();
        for (var index = 0; index < 5; index++)
        {
            records.Add(new BackupRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedUtc = now.AddMinutes(-index),
                Kind = BackupKind.Automatic,
                Name = $"auto {index}",
                ProfileId = Profile.DefaultId
            });
        }

        records.Add(new BackupRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedUtc = now.AddDays(-5),
            Kind = BackupKind.Manual,
            Name = "manual",
            ProfileId = Profile.DefaultId
        });

        records.Add(new BackupRecord
        {
            Id = Guid.NewGuid().ToString("N"),
            CreatedUtc = now.AddDays(-6),
            Kind = BackupKind.PreRestore,
            Name = "pre-restore",
            ProfileId = Profile.DefaultId
        });

        var retention = new RetentionService();
        var expired = retention.SelectExpired(records, new AppSettings { RetentionAutoBackups = 2, RetentionDays = 0 });

        Assert.Equal(3, expired.Count);
        Assert.All(expired, r => Assert.Equal(BackupKind.Automatic, r.Kind));
        Assert.DoesNotContain(expired, r => r.Name is "manual" or "pre-restore");
    }

    [Fact]
    public void Retention_honours_age_and_keeps_manual_backups()
    {
        using var workspace = new TestWorkspace();
        var records = new List<BackupRecord>
        {
            new()
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedUtc = DateTime.UtcNow.AddDays(-40),
                Kind = BackupKind.Automatic,
                Name = "old auto",
                ProfileId = Profile.DefaultId
            },
            new()
            {
                Id = Guid.NewGuid().ToString("N"),
                CreatedUtc = DateTime.UtcNow.AddDays(-40),
                Kind = BackupKind.Manual,
                Name = "old manual",
                ProfileId = Profile.DefaultId
            }
        };

        var expired = new RetentionService().SelectExpired(records, new AppSettings
        {
            RetentionAutoBackups = 10,
            RetentionDays = 30
        });

        var record = Assert.Single(expired);
        Assert.Equal("old auto", record.Name);
    }

    [Fact]
    public void Profiles_are_created_renamed_and_the_default_is_protected()
    {
        using var workspace = new TestWorkspace();
        Assert.Contains(workspace.Profiles.All, p => p.Id == Profile.DefaultId);

        var profile = workspace.Profiles.Create("Steam deck test", "#FF8800");
        Assert.Contains(workspace.Profiles.All, p => p.Id == profile.Id);

        Assert.Throws<InvalidOperationException>(() => workspace.Profiles.Create("Steam deck test", "#000000"));

        workspace.Profiles.Rename(profile.Id, "Handheld");
        Assert.Equal("Handheld", workspace.Profiles.Get(profile.Id).Name);

        Assert.Throws<InvalidOperationException>(() => workspace.Profiles.Delete(Profile.DefaultId));
        workspace.Profiles.Delete(profile.Id);
        Assert.DoesNotContain(workspace.Profiles.All, p => p.Id == profile.Id);
    }

    [Fact]
    public void History_is_searchable_by_text_action_and_status()
    {
        using var workspace = new TestWorkspace();
        workspace.History.Add(new HistoryEntry
        {
            Action = HistoryAction.Backup,
            Status = HistoryStatus.Success,
            Summary = "Backup of Slot1 created",
            Detail = "two files"
        });
        workspace.History.Add(new HistoryEntry
        {
            Action = HistoryAction.Restore,
            Status = HistoryStatus.Failed,
            Summary = "Restore blocked: checksum mismatch"
        });

        Assert.Single(workspace.History.Search("Slot1"));
        Assert.Single(workspace.History.Search(null, HistoryAction.Restore));
        Assert.Single(workspace.History.Search(null, null, HistoryStatus.Failed));
        Assert.Equal(2, workspace.History.Search("").Count);
    }

    [Fact]
    public void FileOps_rejects_traversal_and_reserved_names()
    {
        var root = Path.Combine(Path.GetTempPath(), "ftsm-path-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            Assert.True(FileOps.IsWithin(root, Path.Combine(root, "a", "b.txt")));
            Assert.False(FileOps.IsWithin(root, Path.Combine(root, "..", "b.txt")));
            Assert.Equal("_CON", FileOps.SanitizeFileName("CON"));
            Assert.Equal("a_b", FileOps.SanitizeFileName("a/b"));
            Assert.Equal(".._x", FileOps.SanitizeFileName("..\\x").Replace("_x", "_x"));
            Assert.DoesNotContain(Path.DirectorySeparatorChar, FileOps.SanitizeFileName("weird:name?"));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
