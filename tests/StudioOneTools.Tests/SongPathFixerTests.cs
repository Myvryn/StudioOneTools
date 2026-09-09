using System.IO.Compression;
using System.Xml.Linq;
using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Tests;

public sealed class SongPathFixerTests
{
    [Fact]
    public void FixPaths_ShouldProduceWellFormedXml_WhenFolderNameContainsAmpersand()
    {
        // Reproduces the corruption reported against "The Hungry & the Cold": the current
        // (real, on-disk) project folder contains an ampersand, which previously got spliced
        // into the XML unescaped, breaking well-formedness and making Studio One report the
        // file as corrupted.
        // No spaces here: Studio One writes file:// URLs with literal unencoded spaces, while
        // Uri.AbsoluteUri (used below to build the fixture) percent-encodes them. Keeping the
        // stored path space-free avoids that fixture-only encoding mismatch so the "old prefix"
        // SongPathFixer reconstructs actually matches the text written into the archive.
        const string oldFolderPath = @"D:\OldLocation\Project";

        using var project = TestSongProject.Create("The Hungry & the Cold");

        project.WriteMediaFile(@"Media\Guitar.wav", "guitar");
        project.WriteSongArchive(
            usedAudioClipIds: ["{USED-CLIP}"],
            mediaPoolEntries:
            [
                TestSongProject.MediaPoolEntry.Create("{USED-CLIP}", @"Media\Guitar.wav", 1),
            ],
            documentPathUrl: new Uri(Path.Combine(oldFolderPath, "Project.song")).AbsoluteUri,
            storedFolderPath: oldFolderPath);

        var songFilePath = Path.Combine(project.ProjectFolderPath, $"{new DirectoryInfo(project.ProjectFolderPath).Name}.song");

        var fixer  = new SongPathFixer();
        var result = fixer.FixPaths(songFilePath);

        Assert.True(result.PathsUpdated > 0);

        using var archive = ZipFile.OpenRead(songFilePath);
        var mediaPoolContent = ReadEntry(archive, "Song/mediapool.xml");

        // The regression: this used to throw XmlException because of a bare unescaped '&'.
        var mediaPoolXml = XDocument.Parse(mediaPoolContent);

        var url = mediaPoolXml
            .Descendants("Url")
            .Single()
            .Attribute("url")!
            .Value;

        Assert.Contains("The Hungry & the Cold", Uri.UnescapeDataString(url));
        Assert.Contains("&amp;", mediaPoolContent);
    }

    private static string ReadEntry(ZipArchive archive, string entryName)
    {
        using var stream = archive.GetEntry(entryName)!.Open();
        using var reader = new StreamReader(stream);
        return reader.ReadToEnd();
    }
}
