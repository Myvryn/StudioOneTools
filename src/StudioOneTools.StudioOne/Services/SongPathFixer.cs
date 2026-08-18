using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;

namespace StudioOneTools.StudioOne.Services;

public sealed class SongPathFixer : ISongPathFixer
{
    #region Public Methods

    public SongPathFixAnalysis Analyze(string songFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songFilePath);

        var fullSongPath      = Path.GetFullPath(songFilePath);
        var currentSongFolder = Path.GetDirectoryName(fullSongPath)
            ?? throw new InvalidOperationException("Cannot determine the song folder from the provided path.");

        using var archive = ZipFile.OpenRead(fullSongPath);

        var storedSongFolder = ReadStoredSongFolder(archive);
        var needsFixing      = !string.Equals(
            storedSongFolder.TrimEnd(Path.DirectorySeparatorChar),
            currentSongFolder.TrimEnd(Path.DirectorySeparatorChar),
            StringComparison.OrdinalIgnoreCase);

        var pathsToUpdate = needsFixing
            ? CountOccurrences(archive, ToUrlPrefix(storedSongFolder))
            : 0;

        return new SongPathFixAnalysis
        {
            SongFilePath          = fullSongPath,
            StoredSongFolderPath  = storedSongFolder,
            CurrentSongFolderPath = currentSongFolder,
            NeedsFixing           = needsFixing,
            PathsToUpdate         = pathsToUpdate,
        };
    }

    public SongPathFixResult FixPaths(string songFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songFilePath);

        var analysis = Analyze(songFilePath);

        if (!analysis.NeedsFixing)
        {
            return new SongPathFixResult { SongFilePath = analysis.SongFilePath, PathsUpdated = 0 };
        }

        var oldPrefix    = ToUrlPrefix(analysis.StoredSongFolderPath);
        var newPrefix    = XmlEscape(ToUrlPrefix(analysis.CurrentSongFolderPath));
        var pathsUpdated = RewriteZip(analysis.SongFilePath, oldPrefix, newPrefix);

        return new SongPathFixResult
        {
            SongFilePath  = analysis.SongFilePath,
            PathsUpdated  = pathsUpdated,
        };
    }

    #endregion

    #region Private Methods

    private static string ReadStoredSongFolder(ZipArchive archive)
    {
        var entry = archive.GetEntry("Song/mediapool.xml")
            ?? throw new InvalidDataException("Song/mediapool.xml was not found in the song archive.");

        string content;
        using (var stream = entry.Open())
        using (var reader = new StreamReader(stream, Encoding.UTF8))
        {
            content = reader.ReadToEnd();
        }

        // The documentPath element stores the absolute path to the .song file as it was when last saved.
        // Attribute order can vary, so search for "documentPath" and then extract the url= value.
        var match = Regex.Match(content, @"documentPath[^>]*url=""([^""]+)""");

        if (!match.Success)
        {
            throw new InvalidDataException(
                "Could not find the stored song path (documentPath) in Song/mediapool.xml. " +
                "The file may be corrupted or use an unsupported format.");
        }

        var storedUrl = match.Groups[1].Value;

        // Studio One writes file:// URLs with unencoded spaces; handle both forms.
        if (!Uri.TryCreate(storedUrl, UriKind.Absolute, out var uri))
        {
            Uri.TryCreate(storedUrl.Replace(" ", "%20"), UriKind.Absolute, out uri);
        }

        if (uri is null || !uri.IsFile)
        {
            throw new InvalidDataException($"The stored song path is not a valid file URL: {storedUrl}");
        }

        var localPath = uri.LocalPath.Replace('/', Path.DirectorySeparatorChar);
        return Path.GetDirectoryName(localPath) ?? localPath;
    }

    // Converts a local folder path to the file:// URL prefix used in Studio One's XML.
    // Studio One does not percent-encode spaces, so we replicate that behaviour.
    private static string ToUrlPrefix(string folderPath)
    {
        var normalized = Path.GetFullPath(folderPath)
            .TrimEnd(Path.DirectorySeparatorChar)
            .Replace(Path.DirectorySeparatorChar, '/');

        return "file:///" + normalized + "/";
    }

    // Studio One's XML files may contain any of the five predefined XML entities in a folder
    // name (e.g. "&" in "The Hungry & the Cold"). Because the replacement path comes straight
    // from the filesystem, it must be escaped before being spliced back into XML text, or the
    // result is not well-formed and Studio One will refuse to load the file as "corrupted".
    private static string XmlEscape(string value) =>
        value
            .Replace("&", "&amp;")
            .Replace("<", "&lt;")
            .Replace(">", "&gt;")
            .Replace("\"", "&quot;")
            .Replace("'", "&apos;");

    private static int CountOccurrences(ZipArchive archive, string urlPrefix)
    {
        var escaped = Regex.Escape(urlPrefix);
        var count   = 0;

        foreach (var entry in archive.Entries.Where(IsXmlEntry))
        {
            using var stream  = entry.Open();
            using var reader  = new StreamReader(stream, Encoding.UTF8);
            var       content = reader.ReadToEnd();
            count += Regex.Matches(content, escaped, RegexOptions.IgnoreCase).Count;
        }

        return count;
    }

    private static int RewriteZip(string songFilePath, string oldUrlPrefix, string newUrlPrefix)
    {
        var tempPath     = songFilePath + ".pathtmp";
        var pathsUpdated = 0;
        var escaped      = Regex.Escape(oldUrlPrefix);

        try
        {
            using (var source = ZipFile.OpenRead(songFilePath))
            using (var dest   = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var srcEntry in source.Entries)
                {
                    var destEntry = dest.CreateEntry(srcEntry.FullName, CompressionLevel.Optimal);
                    destEntry.LastWriteTime = srcEntry.LastWriteTime;

                    using var srcStream  = srcEntry.Open();
                    using var destStream = destEntry.Open();

                    if (IsXmlEntry(srcEntry))
                    {
                        using var reader       = new StreamReader(srcStream, Encoding.UTF8);
                        var       content      = reader.ReadToEnd();
                        var       fixedContent = Regex.Replace(content, escaped, _ => newUrlPrefix, RegexOptions.IgnoreCase);

                        if (!string.Equals(content, fixedContent, StringComparison.Ordinal))
                        {
                            pathsUpdated += Regex.Matches(content, escaped, RegexOptions.IgnoreCase).Count;
                        }

                        var bytes = new UTF8Encoding(encoderShouldEmitUTF8Identifier: false).GetBytes(fixedContent);
                        destStream.Write(bytes, 0, bytes.Length);
                    }
                    else
                    {
                        srcStream.CopyTo(destStream);
                    }
                }
            }

            File.Delete(songFilePath);
            File.Move(tempPath, songFilePath);
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                try { File.Delete(tempPath); } catch { /* best-effort cleanup */ }
            }
            throw;
        }

        return pathsUpdated;
    }

    private static bool IsXmlEntry(ZipArchiveEntry entry) =>
        entry.FullName.EndsWith(".xml", StringComparison.OrdinalIgnoreCase);

    #endregion
}
