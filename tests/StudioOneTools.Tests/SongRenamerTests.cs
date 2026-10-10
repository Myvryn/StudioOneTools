using System.IO.Compression;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Tests;

public sealed class SongRenamerTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "sot-renamer-" + Guid.NewGuid().ToString("N"));

    public SongRenamerTests() => Directory.CreateDirectory(_root);

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { /* best effort */ }
    }

    [Fact]
    public async Task Rename_ShouldRenameTheOnlySong_WhenItsNameDiffersFromTheFolder()
    {
        // Reported 2026-10-10: folder "Work Copy" holds "Original Song.song". Renaming the
        // folder used to leave the song (and its Mixdown files) under the old name.
        var folder = MakeFolder("Work Copy", "Original Song");

        var result = await new SongRenamer().RenameAsync(Request(folder, "Work Copy", "New Name"));

        var newFolder = Path.Combine(_root, "New Name");
        Assert.Equal(newFolder, result.NewFolderPath);
        Assert.Empty(result.Errors);
        Assert.True(File.Exists(Path.Combine(newFolder, "New Name.song")));
        Assert.False(File.Exists(Path.Combine(newFolder, "Original Song.song")));
        Assert.True(File.Exists(Path.Combine(newFolder, "Mixdown", "New Name.wav")));
    }

    [Fact]
    public async Task Rename_ShouldOnlyRenameTheMatchingSong_WhenSeveralSongsExist()
    {
        var folder = MakeFolder("Work Copy", "Original Song");
        WriteSong(Path.Combine(folder, "Work Copy.song"));

        await new SongRenamer().RenameAsync(Request(folder, "Work Copy", "New Name"));

        var newFolder = Path.Combine(_root, "New Name");
        Assert.True(File.Exists(Path.Combine(newFolder, "New Name.song")));
        Assert.True(File.Exists(Path.Combine(newFolder, "Original Song.song")));
    }

    [Fact]
    public async Task Rename_ShouldRenameSongAndFolder_WhenNamesAlreadyMatch()
    {
        var folder = MakeFolder("Same", "Same");

        await new SongRenamer().RenameAsync(Request(folder, "Same", "Other"));

        Assert.True(File.Exists(Path.Combine(_root, "Other", "Other.song")));
        Assert.True(File.Exists(Path.Combine(_root, "Other", "Mixdown", "Other.wav")));
    }

    private string MakeFolder(string folderName, string songName)
    {
        var folder = Path.Combine(_root, folderName);
        Directory.CreateDirectory(Path.Combine(folder, "Mixdown"));
        WriteSong(Path.Combine(folder, songName + ".song"));
        File.WriteAllText(Path.Combine(folder, "Mixdown", songName + ".wav"), "x");
        return folder;
    }

    private static void WriteSong(string path)
    {
        using var zip = ZipFile.Open(path, ZipArchiveMode.Create);
        using var writer = new StreamWriter(zip.CreateEntry("metainfo.xml").Open());
        writer.Write("<MetaInformation/>");
    }

    private static SongRenameRequest Request(string folder, string oldName, string newName) =>
        new() { FolderPath = folder, OldName = oldName, NewName = newName };
}
