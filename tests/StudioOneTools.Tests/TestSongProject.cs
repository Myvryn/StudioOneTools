using System.IO.Compression;
using System.Text;

namespace StudioOneTools.Tests;

internal sealed class TestSongProject : IDisposable
{
    #region Fields

    private readonly string _workingRootPath;

    #endregion

    #region Constructors

    private TestSongProject(string workingRootPath, string projectFolderPath)
    {
        _workingRootPath   = workingRootPath;
        ProjectFolderPath  = projectFolderPath;
    }

    #endregion

    #region Properties

    public string ProjectFolderPath { get; }

    #endregion

    #region Public Methods

    public static TestSongProject Create(string projectFolderName = "Project")
    {
        var workingRootPath  = Path.Combine(Path.GetTempPath(), "StudioOneToolsTests", Guid.NewGuid().ToString("N"));
        var projectFolderPath = Path.Combine(workingRootPath, projectFolderName);

        Directory.CreateDirectory(projectFolderPath);

        return new TestSongProject(workingRootPath, projectFolderPath);
    }

    public void WriteMediaFile(string relativePath, string content)
    {
        WriteProjectFile(relativePath, content);
    }

    public void WriteProjectFile(string relativePath, string content)
    {
        var fullPath = Path.Combine(ProjectFolderPath, ToHostRelativePath(relativePath));
        var directoryPath = Path.GetDirectoryName(fullPath)
            ?? throw new InvalidOperationException("A directory path was expected.");

        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(fullPath, content, Encoding.UTF8);
    }

    public void WriteSongArchive(
        IReadOnlyCollection<string> usedAudioClipIds,
        IReadOnlyCollection<MediaPoolEntry> mediaPoolEntries,
        string? documentPathUrl = null,
        string? storedFolderPath = null)
    {
        var songFilePath = Path.Combine(ProjectFolderPath, $"{new DirectoryInfo(ProjectFolderPath).Name}.song");

        using var archive = ZipFile.Open(songFilePath, ZipArchiveMode.Create);

        WriteArchiveEntry(archive, "Song/song.xml", CreateSongXml(usedAudioClipIds));
        WriteArchiveEntry(archive, "Song/mediapool.xml", CreateMediaPoolXml(mediaPoolEntries, storedFolderPath ?? ProjectFolderPath, documentPathUrl));
    }

    public string GetArchiveFilePath(string fileName)
    {
        return Path.Combine(_workingRootPath, fileName);
    }

    public void Dispose()
    {
        if (Directory.Exists(_workingRootPath))
        {
            Directory.Delete(_workingRootPath, recursive: true);
        }
    }

    #endregion

    #region Private Methods

    // Test fixtures write relative paths with a literal '\' (matching the production
    // code's own internal canonical relative-path format -- see SongFolderArchiver/
    // StudioOneSongAnalyzer's NormalizeRelativePath). That's just string manipulation
    // there, but here it has to become a real path the OS's File/Directory APIs can
    // resolve, which only means the same thing as '\' on Windows -- on macOS/Linux it's
    // an ordinary filename character, so Path.Combine never sees a subfolder at all.
    // '/' is accepted by .NET's Path APIs as a separator on every OS, Windows included.
    private static string ToHostRelativePath(string relativePath) => relativePath.Replace('\\', '/');

    // Builds a file:// URL the same way SongPathFixer's own ToUrlPrefix does (plain string
    // concatenation, not System.Uri), because these fixtures deliberately simulate paths from
    // an arbitrary OS (a Windows drive-letter path, say, "D:/OldLocation/Project") that may not
    // be the one actually running the test -- new Uri(path)'s LocalPath/AbsoluteUri handling of
    // a drive letter is not the same on every OS, only string manipulation is. Matches real
    // Studio One output too: literal unencoded spaces, no percent-escaping.
    internal static string ToFileUrl(string absolutePath) => "file:///" + absolutePath.Replace('\\', '/').TrimStart('/');

    private static void WriteArchiveEntry(ZipArchive archive, string entryPath, string content)
    {
        var entry = archive.CreateEntry(entryPath, CompressionLevel.NoCompression);

        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static string CreateSongXml(IReadOnlyCollection<string> usedAudioClipIds)
    {
        var builder = new StringBuilder();

        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        builder.AppendLine("""<Song xmlns:x="urn:test">""");
        builder.AppendLine("""  <List x:id="Tracks">""");
        builder.AppendLine("""    <MediaTrack mediaType="Audio">""");
        builder.AppendLine("""      <List x:id="Events">""");

        foreach (var usedAudioClipId in usedAudioClipIds)
        {
            builder.AppendLine($"""        <AudioEvent clipID="{usedAudioClipId}" start="0" timeFormat="2" length="8" name="Used Clip" />""");
        }

        builder.AppendLine("""      </List>""");
        builder.AppendLine("""    </MediaTrack>""");
        builder.AppendLine("""  </List>""");
        builder.AppendLine("""</Song>""");

        return builder.ToString();
    }

    private static string CreateMediaPoolXml(IReadOnlyCollection<MediaPoolEntry> mediaPoolEntries, string projectFolderPath, string? documentPathUrl)
    {
        var builder = new StringBuilder();

        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        builder.AppendLine("""<MediaPool xmlns:x="urn:test">""");
        builder.AppendLine("""  <Attributes x:id="rootFolder">""");
        builder.AppendLine("""    <MediaFolder name="Audio">""");

        foreach (var mediaPoolEntry in mediaPoolEntries)
        {
            var absolutePath = projectFolderPath.Replace('\\', '/').TrimEnd('/') + "/" + ToHostRelativePath(mediaPoolEntry.RelativePath);
            var mediaFileUrl = ToFileUrl(absolutePath);

            builder.AppendLine($"""      <AudioClip mediaID="{mediaPoolEntry.MediaId}" useCount="{mediaPoolEntry.UseCount}">""");
            builder.AppendLine($"""        <Url x:id="path" type="1" url="{mediaFileUrl}" />""");
            builder.AppendLine("""      </AudioClip>""");
        }

        builder.AppendLine("""    </MediaFolder>""");
        builder.AppendLine("""  </Attributes>""");

        if (documentPathUrl is not null)
        {
            builder.AppendLine($"""  <Attributes x:id="documentPath" type="1" url="{documentPathUrl}"/>""");
        }

        builder.AppendLine("""</MediaPool>""");

        return builder.ToString();
    }

    #endregion

    #region Nested Types

    internal sealed record MediaPoolEntry(string MediaId, string RelativePath, int UseCount)
    {
        public static MediaPoolEntry Create(string mediaId, string relativePath, int useCount)
        {
            return new MediaPoolEntry(mediaId, relativePath, useCount);
        }
    }

    #endregion
}
