using System.IO.Compression;
using System.Text;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;

namespace StudioOneTools.StudioOne.Services;

public sealed class SongFolderArchiver : ISongFolderArchiver
{
    #region Fields

    private readonly IStudioOneSongAnalyzer _songAnalyzer;

    #endregion

    #region Constructors

    public SongFolderArchiver(IStudioOneSongAnalyzer songAnalyzer)
    {
        _songAnalyzer = songAnalyzer ?? throw new ArgumentNullException(nameof(songAnalyzer));
    }

    #endregion

    #region Public Methods

    public SongArchiveResult CreateArchive(SongArchiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.SongFolderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ArchiveFilePath);

        var songFolderPath  = Path.GetFullPath(request.SongFolderPath);
        var archiveFilePath = Path.GetFullPath(request.ArchiveFilePath);

        if (!Directory.Exists(songFolderPath))
        {
            throw new DirectoryNotFoundException($"Song folder was not found: {songFolderPath}");
        }

        if (IsWithinDirectory(songFolderPath, archiveFilePath))
        {
            throw new InvalidOperationException("The archive file cannot be saved inside the song folder.");
        }

        var archiveDirectoryPath = Path.GetDirectoryName(archiveFilePath)
            ?? throw new InvalidOperationException("The archive file path must include a directory.");

        Directory.CreateDirectory(archiveDirectoryPath);

        var analysis = _songAnalyzer.Analyze(songFolderPath, request.SongFilePath);

        if (analysis.HasMissingReferencedFiles)
        {
            var missingFiles = string.Join(Environment.NewLine, analysis.MissingReferencedFiles.Select(file => file.RelativePath));

            throw new InvalidOperationException($"Cannot create archive because referenced media files are missing:{Environment.NewLine}{missingFiles}");
        }

        var usedWaveFiles = analysis.UsedWaveFiles
            .Select(file => NormalizeRelativePath(file.RelativePath))
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var includedEntries = new List<string>();
        var excludedEntries = new List<string>();
        var sourceFiles     = Directory.GetFiles(songFolderPath, "*", SearchOption.AllDirectories)
            .OrderBy(filePath => filePath, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        using var archiveStream = new FileStream(archiveFilePath, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        using var zipArchive    = new ZipArchive(archiveStream, ZipArchiveMode.Create);

        foreach (var sourceFilePath in sourceFiles)
        {
            var relativePath = NormalizeRelativePath(Path.GetRelativePath(songFolderPath, sourceFilePath));

            if (!ShouldIncludeFile(relativePath, usedWaveFiles, request))
            {
                excludedEntries.Add(relativePath);
                continue;
            }

            zipArchive.CreateEntryFromFile(sourceFilePath, relativePath.Replace('\\', '/'), CompressionLevel.Optimal);
            includedEntries.Add(relativePath);
        }

        var readmeEntry = zipArchive.CreateEntry("Song_Information.html", CompressionLevel.Optimal);

        using (var readmeWriter = new StreamWriter(readmeEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
        {
            readmeWriter.Write(SongReadmeGenerator.Generate(analysis));
        }

        includedEntries.Add("Song_Information.html");

        // Include XML schema discovery report for debugging (only if debug mode enabled)
        if (request.DebugMode)
        {
            try
            {
                var schemaReport = _songAnalyzer.DiscoverSongStructure(request.SongFilePath ?? analysis.SongFilePath);
                var schemaEntry = zipArchive.CreateEntry("_DEBUG_Song_Schema.txt", CompressionLevel.Optimal);

                using (var schemaWriter = new StreamWriter(schemaEntry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false)))
                {
                    schemaWriter.Write(schemaReport);
                }

                includedEntries.Add("_DEBUG_Song_Schema.txt");
            }
            catch
            {
                // Schema generation is not critical, so don't fail the archive if it fails
            }

            // Include all XML files from the .song archive for debugging
            try
            {
                using var songArchive = ZipFile.OpenRead(request.SongFilePath ?? analysis.SongFilePath);
                foreach (var entry in songArchive.Entries.Where(e => e.Name.EndsWith(".xml", StringComparison.OrdinalIgnoreCase)))
                {
                    var debugPath = $"_DEBUG_XML_Files/{entry.FullName}";
                    var debugEntry = zipArchive.CreateEntry(debugPath, CompressionLevel.Optimal);

                    using (var sourceStream = entry.Open())
                    using (var destStream = debugEntry.Open())
                    {
                        sourceStream.CopyTo(destStream);
                    }

                    includedEntries.Add(debugPath);
                }
            }
            catch
            {
                // XML extraction is not critical, so don't fail the archive if it fails
            }
        }

        return new SongArchiveResult
        {
            ArchiveFilePath = archiveFilePath,
            Analysis        = analysis,
            IncludedEntries = includedEntries,
            ExcludedEntries = excludedEntries,
        };
    }

    #endregion

    #region Private Methods

    private static bool ShouldIncludeFile(
        string relativePath,
        HashSet<string> usedWaveFiles,
        SongArchiveRequest request)
    {
        var rootSegment = GetRootSegment(relativePath);

        if (!request.RetainMixdownFiles &&
            string.Equals(rootSegment, "Mixdown", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!request.RetainMasterFiles &&
            string.Equals(rootSegment, "Master", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (relativePath.StartsWith("Media\\", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(Path.GetExtension(relativePath), ".wav", StringComparison.OrdinalIgnoreCase))
        {
            var mediaRelativePath = NormalizeRelativePath(relativePath["Media\\".Length..]);

            return usedWaveFiles.Contains(mediaRelativePath) || request.IncludeUnusedMediaFiles;
        }

        return true;
    }

    private static string GetRootSegment(string relativePath)
    {
        var separatorIndex = relativePath.IndexOf('\\');

        return separatorIndex < 0
            ? relativePath
            : relativePath[..separatorIndex];
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('/', '\\');
    }

    private static bool IsWithinDirectory(string directoryPath, string candidatePath)
    {
        var normalizedDirectoryPath = EnsureTrailingSeparator(Path.GetFullPath(directoryPath));
        var normalizedCandidatePath = Path.GetFullPath(candidatePath);

        return normalizedCandidatePath.StartsWith(normalizedDirectoryPath, StringComparison.OrdinalIgnoreCase);
    }

    private static string EnsureTrailingSeparator(string path)
    {
        return path.EndsWith(Path.DirectorySeparatorChar)
            ? path
            : path + Path.DirectorySeparatorChar;
    }

    #endregion
}
