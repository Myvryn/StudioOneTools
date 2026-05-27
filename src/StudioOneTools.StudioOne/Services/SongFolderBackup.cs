using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;

namespace StudioOneTools.StudioOne.Services;

public sealed class SongFolderBackup : ISongFolderBackup
{
    #region Fields

    private readonly IStudioOneSongAnalyzer _analyzer;

    #endregion

    #region Constructors

    public SongFolderBackup(IStudioOneSongAnalyzer analyzer)
    {
        _analyzer = analyzer;
    }

    #endregion

    #region Public Methods

    public IReadOnlyList<SongBackupItem> GetSongFolders(string rootFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootFolderPath);

        var normalizedPath = Path.GetFullPath(rootFolderPath);

        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"Folder was not found: {normalizedPath}");
        }

        return Directory.GetDirectories(normalizedPath, "*", SearchOption.TopDirectoryOnly)
            .Where(dir => Directory.GetFiles(dir, "*.song", SearchOption.TopDirectoryOnly).Length > 0)
            .OrderBy(dir => dir, StringComparer.OrdinalIgnoreCase)
            .Select(dir => new SongBackupItem
            {
                FolderPath = dir,
                FolderName = Path.GetFileName(dir),
            })
            .ToArray();
    }

    public Task<SongBackupPlan> PlanBackupAsync(IReadOnlyList<string> sourceFolderPaths, string backupRootPath)
    {
        ArgumentNullException.ThrowIfNull(sourceFolderPaths);
        ArgumentException.ThrowIfNullOrWhiteSpace(backupRootPath);

        return Task.Run(() => BuildPlan(sourceFolderPaths, backupRootPath));
    }

    public Task<SongBackupResult> ExecuteBackupAsync(
        SongBackupPlan     plan,
        bool               includeUnusedFiles,
        IProgress<string>? progress = null)
    {
        ArgumentNullException.ThrowIfNull(plan);

        return Task.Run(() => ExecuteBackup(plan, includeUnusedFiles, progress));
    }

    #endregion

    #region Private Methods

    private SongBackupPlan BuildPlan(IReadOnlyList<string> sourceFolderPaths, string backupRootPath)
    {
        var normalizedBackupRoot = Path.GetFullPath(backupRootPath);
        var planFolders          = new List<SongBackupPlanFolder>(sourceFolderPaths.Count);

        foreach (var sourceFolder in sourceFolderPaths)
        {
            var folderName  = Path.GetFileName(sourceFolder);
            var destFolder  = Path.Combine(normalizedBackupRoot, folderName);
            var unusedPaths = ResolveUnusedAudioPaths(sourceFolder);
            var filesToCopy = new List<SongBackupFile>();

            foreach (var sourceFile in Directory.EnumerateFiles(sourceFolder, "*", SearchOption.AllDirectories))
            {
                var relativePath = Path.GetRelativePath(sourceFolder, sourceFile);
                var destFile     = Path.Combine(destFolder, relativePath);

                bool isNew;
                bool shouldCopy;

                if (!File.Exists(destFile))
                {
                    isNew      = true;
                    shouldCopy = true;
                }
                else
                {
                    isNew      = false;
                    shouldCopy = File.GetLastWriteTimeUtc(sourceFile) > File.GetLastWriteTimeUtc(destFile);
                }

                if (shouldCopy)
                {
                    filesToCopy.Add(new SongBackupFile
                    {
                        SourcePath      = sourceFile,
                        DestinationPath = destFile,
                        RelativePath    = relativePath,
                        IsNew           = isNew,
                        IsUnusedAudio   = unusedPaths.Contains(sourceFile),
                    });
                }
            }

            planFolders.Add(new SongBackupPlanFolder
            {
                SourceFolderPath      = sourceFolder,
                DestinationFolderPath = destFolder,
                FolderName            = folderName,
                FilesToCopy           = filesToCopy,
            });
        }

        return new SongBackupPlan
        {
            BackupRootPath = normalizedBackupRoot,
            Folders        = planFolders,
        };
    }

    private HashSet<string> ResolveUnusedAudioPaths(string folderPath)
    {
        try
        {
            return _analyzer.Analyze(folderPath)
                .UnusedWaveFiles
                .Select(f => f.FullPath)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    private static SongBackupResult ExecuteBackup(
        SongBackupPlan     plan,
        bool               includeUnusedFiles,
        IProgress<string>? progress)
    {
        var filesCopied = 0;
        var errors      = new List<string>();

        foreach (var folder in plan.Folders)
        {
            foreach (var file in folder.FilesToCopy)
            {
                if (!includeUnusedFiles && file.IsUnusedAudio)
                {
                    continue;
                }

                try
                {
                    progress?.Report($"{folder.FolderName}: {file.RelativePath}");
                    Directory.CreateDirectory(Path.GetDirectoryName(file.DestinationPath)!);
                    File.Copy(file.SourcePath, file.DestinationPath, overwrite: true);
                    filesCopied++;
                }
                catch (Exception ex)
                {
                    errors.Add($"{file.RelativePath}: {ex.Message}");
                }
            }
        }

        return new SongBackupResult
        {
            FilesCopied      = filesCopied,
            FoldersProcessed = plan.Folders.Count,
            Errors           = errors,
        };
    }

    #endregion
}
