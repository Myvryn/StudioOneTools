using System.IO.Compression;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Tests;

public sealed class SongFolderArchiverTests
{
    [Fact]
    public void CreateArchive_ShouldExcludeUnusedWaveFilesAndRetainOtherContentByDefault()
    {
        using var project = TestSongProject.Create();

        project.WriteMediaFile(@"Media\used.wav", "used");
        project.WriteMediaFile(@"Media\unused.wav", "unused");
        project.WriteProjectFile(@"Mixdown\mix.wav", "mixdown");
        project.WriteProjectFile(@"Master\master.wav", "master");
        project.WriteProjectFile(@"notes.txt", "notes");
        project.WriteSongArchive(
            usedAudioClipIds: ["{USED-CLIP}"],
            mediaPoolEntries:
            [
                TestSongProject.MediaPoolEntry.Create("{USED-CLIP}",  @"Media\used.wav",   1),
                TestSongProject.MediaPoolEntry.Create("{UNUSED-CLIP}",@"Media\unused.wav", 0),
            ]);

        var analyzer = new StudioOneSongAnalyzer();
        var archiver = new SongFolderArchiver(analyzer);
        var result   = archiver.CreateArchive(new SongArchiveRequest
        {
            SongFolderPath     = project.ProjectFolderPath,
            ArchiveFilePath    = project.GetArchiveFilePath("default.zip"),
            RetainMixdownFiles = true,
            RetainMasterFiles  = true,
        });

        using var archive = ZipFile.OpenRead(result.ArchiveFilePath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

        Assert.Contains("Media/used.wav", entries);
        Assert.DoesNotContain("Media/unused.wav", entries);
        Assert.Contains("Mixdown/mix.wav", entries);
        Assert.Contains("Master/master.wav", entries);
        Assert.Contains("notes.txt", entries);
    }

    [Fact]
    public void CreateArchive_ShouldHonorMixdownAndMasterOptions()
    {
        using var project = TestSongProject.Create();

        project.WriteMediaFile(@"Media\used.wav", "used");
        project.WriteProjectFile(@"Mixdown\mix.wav", "mixdown");
        project.WriteProjectFile(@"Master\master.wav", "master");
        project.WriteSongArchive(
            usedAudioClipIds: ["{USED-CLIP}"],
            mediaPoolEntries:
            [
                TestSongProject.MediaPoolEntry.Create("{USED-CLIP}", @"Media\used.wav", 1),
            ]);

        var analyzer = new StudioOneSongAnalyzer();
        var archiver = new SongFolderArchiver(analyzer);
        var result   = archiver.CreateArchive(new SongArchiveRequest
        {
            SongFolderPath     = project.ProjectFolderPath,
            ArchiveFilePath    = project.GetArchiveFilePath("trimmed.zip"),
            RetainMixdownFiles = false,
            RetainMasterFiles  = false,
        });

        using var archive = ZipFile.OpenRead(result.ArchiveFilePath);
        var entries = archive.Entries.Select(entry => entry.FullName).ToArray();

        Assert.Contains("Media/used.wav", entries);
        Assert.DoesNotContain("Mixdown/mix.wav", entries);
        Assert.DoesNotContain("Master/master.wav", entries);
    }

    [Fact]
    public void CreateArchive_ShouldRejectArchiveInsideSongFolder()
    {
        using var project = TestSongProject.Create();

        project.WriteMediaFile(@"Media\used.wav", "used");
        project.WriteSongArchive(
            usedAudioClipIds: ["{USED-CLIP}"],
            mediaPoolEntries:
            [
                TestSongProject.MediaPoolEntry.Create("{USED-CLIP}", @"Media\used.wav", 1),
            ]);

        var analyzer = new StudioOneSongAnalyzer();
        var archiver = new SongFolderArchiver(analyzer);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            archiver.CreateArchive(new SongArchiveRequest
            {
                SongFolderPath     = project.ProjectFolderPath,
                ArchiveFilePath    = Path.Combine(project.ProjectFolderPath, "inside.zip"),
                RetainMixdownFiles = true,
                RetainMasterFiles  = true,
            }));

        Assert.Contains("inside the song folder", exception.Message, StringComparison.OrdinalIgnoreCase);
    }
}
