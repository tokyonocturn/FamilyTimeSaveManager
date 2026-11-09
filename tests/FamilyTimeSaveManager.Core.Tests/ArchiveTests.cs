using System.IO.Compression;
using System.Text;
using FamilyTimeSaveManager.Core.Models;
using FamilyTimeSaveManager.Core.Services;
using Xunit;

namespace FamilyTimeSaveManager.Core.Tests;

public class ArchiveTests
{
    private static byte[] SaveContent(byte marker) => SaveBytes.Episode3(new[]
    {
        ("session.player", new[] { marker, marker, marker })
    });

    [Fact]
    public void Export_and_import_round_trips_content_and_hashes()
    {
        using var source = new TestWorkspace();
        var savePath = source.Write("Sessions/Slot1.family", SaveContent(5));
        var record = source.Backups.Create(
            source.Describe(savePath), "Archive test", "desc", Profile.DefaultId, BackupKind.Manual);

        var archivePath = Path.Combine(source.Root, "export.zip");
        var export = source.Archives.Export(new[] { record }, archivePath);

        Assert.True(File.Exists(archivePath));
        Assert.Equal(1, export.BackupCount);
        Assert.True(export.ArchiveBytes > 0);

        using var destination = new TestWorkspace();
        var import = destination.Archives.Import(archivePath);

        Assert.Single(import.Imported);
        var imported = import.Imported[0];
        Assert.Equal(BackupKind.Imported, imported.Kind);
        Assert.True(imported.Verified);
        Assert.NotEqual(record.Id, imported.Id);
        Assert.Equal("export.zip", imported.ImportedFrom);

        var verification = destination.Backups.Verify(imported);
        Assert.True(verification.Ok, string.Join(" | ", verification.Errors));
        Assert.Equal(record.PrimaryFile!.Sha256, imported.PrimaryFile!.Sha256);

        var restored = Path.Combine(destination.Root, "restored.family");
        Directory.CreateDirectory(Path.GetDirectoryName(restored)!);
        var restore = destination.Restores.Restore(imported, new RestoreOptions { DestinationFolder = Path.GetDirectoryName(restored) });
        Assert.True(restore.Success, restore.Message);
        Assert.Equal(SaveContent(5), File.ReadAllBytes(Path.Combine(Path.GetDirectoryName(restored)!, "Slot1.family")));
    }

    [Fact]
    public void Import_rejects_parent_traversal_entries()
    {
        using var workspace = new TestWorkspace();
        var archivePath = Path.Combine(workspace.Root, "evil.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("../escape.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("escaped");
        }

        var exception = Assert.Throws<ArchiveException>(() => workspace.Archives.Import(archivePath));
        Assert.Contains("parent", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(Directory.GetFiles(workspace.Root, "escape.txt", SearchOption.AllDirectories));
        Assert.Empty(workspace.Repository.List());
    }

    [Fact]
    public void Import_rejects_absolute_entry_paths()
    {
        using var workspace = new TestWorkspace();
        var archivePath = Path.Combine(workspace.Root, "absolute.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("C:/Windows/System32/evil.dll");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("nope");
        }

        var exception = Assert.Throws<ArchiveException>(() => workspace.Archives.Import(archivePath));
        Assert.Contains("absolute", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(workspace.Repository.List());
    }

    [Fact]
    public void Import_rejects_a_manifest_whose_file_hash_does_not_match()
    {
        using var workspace = new TestWorkspace();
        var archivePath = Path.Combine(workspace.Root, "tampered.zip");
        var id = new string('a', 32);

        var record = new BackupRecord
        {
            Id = id,
            Name = "Forged",
            ProfileId = Profile.DefaultId,
            Episode = Episode.Episode3,
            Verified = true,
            Files =
            {
                new BackupFileEntry
                {
                    StoredName = "Slot1.family",
                    OriginalFileName = "Slot1.family",
                    OriginalPath = @"C:\Games\Family Time\Sessions\Slot1.family",
                    Sha256 = new string('f', 64),
                    Length = 3,
                    Role = "primary"
                }
            }
        };

        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var manifest = archive.CreateEntry($"{id}/manifest.json");
            using (var writer = new StreamWriter(manifest.Open()))
            {
                writer.Write(System.Text.Json.JsonSerializer.Serialize(record, Core.Storage.JsonFile.Options));
            }

            var file = archive.CreateEntry($"{id}/files/Slot1.family");
            using var stream = file.Open();
            stream.Write(new byte[] { 1, 2, 3 });
        }

        var exception = Assert.Throws<ArchiveException>(() => workspace.Archives.Import(archivePath));
        Assert.Contains("SHA-256", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(workspace.Repository.List());
    }

    [Fact]
    public void Import_rejects_a_non_zip_file()
    {
        using var workspace = new TestWorkspace();
        var path = workspace.Write("not-a-zip.zip", Encoding.UTF8.GetBytes("definitely not a zip archive"));

        Assert.ThrowsAny<Exception>(() => workspace.Archives.Import(path));
        Assert.Empty(workspace.Repository.List());
    }
}
