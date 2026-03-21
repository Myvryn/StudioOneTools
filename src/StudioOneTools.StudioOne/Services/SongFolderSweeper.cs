using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;

namespace StudioOneTools.StudioOne.Services;

public sealed class SongFolderSweeper : ISongFolderSweeper
{
    #region Fields

    private static readonly TimeSpan StaleThreshold = TimeSpan.FromDays(365);

    #endregion

    #region Public Methods

    public IReadOnlyList<SweepFolderResult> Sweep(string rootFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolderPath);

        var normalizedPath = Path.GetFullPath(rootFolderPath);

        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"Folder was not found: {normalizedPath}");
        }

        var results = new List<SweepFolderResult>();

        foreach (var folderPath in Directory.GetDirectories(normalizedPath, "*", SearchOption.TopDirectoryOnly)
            .OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            var reason = EvaluateFolder(folderPath);

            if (reason is not null)
            {
                results.Add(new SweepFolderResult
                {
                    FolderPath = folderPath,
                    FolderName = Path.GetFileName(folderPath),
                    Reason     = reason,
                });
            }
        }

        return results;
    }

    #endregion

    #region Private Methods

    private static string? EvaluateFolder(string folderPath)
    {
        var songFiles = Directory.GetFiles(folderPath, "*.song", SearchOption.TopDirectoryOnly);

        if (songFiles.Length == 0)
        {
            return "No .song file found.";
        }

        var hasMixdown = Directory.Exists(Path.Combine(folderPath, "Mixdown"));
        var hasMaster  = Directory.Exists(Path.Combine(folderPath, "Master"));

        if (!hasMixdown && !hasMaster)
        {
            return "No Mixdown or Master folder found.";
        }

        var newestSong   = songFiles.OrderByDescending(File.GetLastWriteTime).First();
        var lastModified = File.GetLastWriteTime(newestSong);

        if (DateTime.Now - lastModified > StaleThreshold)
        {
            return $"Song not modified in over a year (last modified {lastModified:MMM d, yyyy}).";
        }

        return null;
    }

    #endregion
}
