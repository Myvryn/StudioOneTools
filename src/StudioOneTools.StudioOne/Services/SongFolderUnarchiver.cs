using System.IO.Compression;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;

namespace StudioOneTools.StudioOne.Services;

public sealed class SongFolderUnarchiver : ISongFolderUnarchiver
{
    private readonly ISongPathFixer _pathFixer;

    public SongFolderUnarchiver(ISongPathFixer pathFixer)
    {
        _pathFixer = pathFixer;
    }

    public SongUnarchiveResult Unarchive(SongUnarchiveRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.ArchiveFilePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.DestinationFolder);

        if (!File.Exists(request.ArchiveFilePath))
            throw new FileNotFoundException("Archive file not found.", request.ArchiveFilePath);

        if (!Directory.Exists(request.DestinationFolder))
            throw new DirectoryNotFoundException($"Destination folder not found: {request.DestinationFolder}");

        var songFolderName = Path.GetFileNameWithoutExtension(request.ArchiveFilePath);
        var songFolderPath = Path.Combine(Path.GetFullPath(request.DestinationFolder), songFolderName);

        if (Directory.Exists(songFolderPath))
            throw new InvalidOperationException($"A folder named \"{songFolderName}\" already exists at the destination.");

        Directory.CreateDirectory(songFolderPath);

        try
        {
            ZipFile.ExtractToDirectory(request.ArchiveFilePath, songFolderPath);
        }
        catch
        {
            // Clean up the partial extraction before surfacing the error
            try { Directory.Delete(songFolderPath, recursive: true); } catch { /* best-effort */ }
            throw;
        }

        var songFilePath = Directory
            .GetFiles(songFolderPath, "*.song", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTime)
            .FirstOrDefault()
            ?? throw new FileNotFoundException("No .song file was found in the extracted archive.");

        var fixResult = _pathFixer.FixPaths(songFilePath);

        return new SongUnarchiveResult
        {
            SongFolderPath = songFolderPath,
            SongFilePath   = songFilePath,
            PathsFixed     = fixResult.PathsUpdated,
        };
    }
}
