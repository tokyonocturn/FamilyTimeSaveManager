using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Services;
using FamilyTimeSaveManager.Core.Storage;
using Xunit;

namespace FamilyTimeSaveManager.Core.Tests;

public class BackupRestoreTests
{
    private static byte[] SaveContent(byte marker) => SaveBytes.Episode3(new[]
    {
        ("session.player", new[] { marker, marker, marker, marker })
    });

    [Fact]
    public void Backup_copies_primary_and_sidecar_and_records_hashes()
    {
        using var workspace = new TestWorkspace();
        var savePath = workspace.Write("Sessions/Slot1.family", SaveContent(1));
        var sidecarPath = workspace.Write("Sessions/Slot1.bpm", new byte[] { 8, 8, 8 });
        var save = workspace.Describe(savePath, sidecars: new[] { sidecarPath });

        var record = workspace.Backups.Create(save, "Before experiment", "desc", Profile.DefaultId, BackupKind.Manual);

        Assert.Equal(2, record.Files.Count);
        Assert.True(record.Verified);
        Assert.NotNull(record.PrimaryFile);
        Assert.Equal(record.TotalBytes, record.Files.Sum(f => f.Length));
        Assert.All(record.Files, f => Assert.Equal(64, f.Sha256.Length));

        var verification = workspace.Backups.Verify(record);
        Assert.True(verification.Ok, string.Join(" | ", verification.Errors));
        Assert.True(File.Exists(Path.Combine(workspace.Paths.BackupFilesFolder(record.Id), "Slot1.family")));
    }

    [Fact]
    public void Tampered_backup_fails_verification_and_blocks_restore()
    {
        using var workspace = new TestWorkspace();
        var savePath = workspace.Write("Sessions/Slot1.family", SaveContent(1));
        var save = workspace.Describe(savePath);
        var record = workspace.Backups.Create(save, "Original", null, Profile.DefaultId, BackupKind.Manual);

        var stored = Path.Combine(workspace.Paths.BackupFilesFolder(record.Id), "Slot1.family");
        var bytes = File.ReadAllBytes(stored);
        bytes[^1] ^= 0x7F;
        File.WriteAllBytes(stored, bytes);

        var verification = workspace.Backups.Verify(record);
        Assert.False(verification.Ok);
        Assert.Contains(verification.Errors, e => e.Contains("SHA-256", StringComparison.OrdinalIgnoreCase));

        var originalLive = File.ReadAllBytes(savePath);
        var result = workspace.Restores.Restore(record);

        Assert.False(result.Success);
        Assert.Equal(originalLive, File.ReadAllBytes(savePath));
        Assert.Empty(result.WrittenFiles);
    }

    [Fact]
    public void Restore_creates_a_pre_restore_backup_of_the_live_file()
    {
        using var workspace = new TestWorkspace();
        var savePath = workspace.Write("Sessions/Slot1.family", SaveContent(1));
        var record = workspace.Backups.Create(
            workspace.Describe(savePath), "Snapshot A", null, Profile.DefaultId, BackupKind.Manual);

        var modified = SaveContent(2);
        File.WriteAllBytes(savePath, modified);

        var result = workspace.Restores.Restore(record);

        Assert.True(result.Success, result.Message);
        Assert.NotNull(result.PreRestoreBackupId);
        Assert.Equal(SaveContent(1), File.ReadAllBytes(savePath));

        var preRestore = workspace.Repository.Get(result.PreRestoreBackupId!);
        Assert.NotNull(preRestore);
        Assert.Equal(BackupKind.PreRestore, preRestore!.Kind);

        var preRestoreFile = Path.Combine(
            workspace.Paths.BackupFilesFolder(preRestore.Id),
            preRestore.PrimaryFile!.StoredName);
        Assert.Equal(modified, File.ReadAllBytes(preRestoreFile));
    }

    [Fact]
    public void Restore_refuses_a_missing_destination_folder()
    {
        using var workspace = new TestWorkspace();
        var savePath = workspace.Write("Sessions/Slot1.family", SaveContent(1));
        var record = workspace.Backups.Create(
            workspace.Describe(savePath), "Snapshot", null, Profile.DefaultId, BackupKind.Manual);

        var options = new RestoreOptions { DestinationFolder = Path.Combine(workspace.Root, "not-here") };
        var result = workspace.Restores.Restore(record, options);

        Assert.False(result.Success);
        Assert.Contains("does not exist", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(result.WrittenFiles);
    }

    [Fact]
    public void Backup_of_a_missing_file_leaves_no_partial_folder()
    {
        using var workspace = new TestWorkspace();
        var missing = Path.Combine(workspace.Root, "Sessions", "Gone.family");
        var fake = new SaveFileInfo
        {
            Path = missing,
            Episode = Episode.Episode3,
            Sha256 = new string('0', 64)
        };

        Assert.Throws<FileNotFoundException>(() =>
            workspace.Backups.Create(fake, "Nope", null, Profile.DefaultId, BackupKind.Manual));

        Assert.Empty(workspace.Repository.List());
        Assert.Empty(Directory.GetDirectories(workspace.Paths.BackupsRoot));
    }

    [Fact]
    public void Backup_names_are_sanitised_and_default_to_the_save_name()
    {
        using var workspace = new TestWorkspace();
        var savePath = workspace.Write("Sessions/Weird.family", SaveContent(3));
        var record = workspace.Backups.Create(
            workspace.Describe(savePath), "   ", null, Profile.DefaultId, BackupKind.Manual);

        Assert.False(string.IsNullOrWhiteSpace(record.Name));
        Assert.StartsWith("Weird", record.Name);

        var stored = Path.Combine(workspace.Paths.BackupFilesFolder(record.Id), record.PrimaryFile!.StoredName);
        Assert.True(FileOps.IsWithin(workspace.Paths.BackupFilesFolder(record.Id), stored));
    }

    [Fact]
    public void Deleting_a_backup_moves_it_to_trash_instead_of_destroying_it()
    {
        using var workspace = new TestWorkspace();
        var savePath = workspace.Write("Sessions/Slot1.family", SaveContent(4));
        var record = workspace.Backups.Create(
            workspace.Describe(savePath), "Trash me", null, Profile.DefaultId, BackupKind.Manual);

        workspace.Repository.MoveToTrash(record.Id);

        Assert.Null(workspace.Repository.Get(record.Id));
        Assert.True(File.Exists(savePath));
        var trash = Path.Combine(workspace.Paths.BackupsRoot, ".trash");
        Assert.NotEmpty(Directory.GetDirectories(trash));
    }
}
