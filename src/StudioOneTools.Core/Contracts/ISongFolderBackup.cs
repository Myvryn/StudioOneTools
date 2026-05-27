using StudioOneTools.Core.Models;

namespace StudioOneTools.Core.Contracts;

public interface ISongFolderBackup
{
    IReadOnlyList<SongBackupItem> GetSongFolders(string rootFolderPath);

    Task<SongBackupPlan> PlanBackupAsync(IReadOnlyList<string> sourceFolderPaths, string backupRootPath);

    Task<SongBackupResult> ExecuteBackupAsync(
        SongBackupPlan plan,
        bool           includeUnusedFiles,
        IProgress<string>? progress = null);
}
