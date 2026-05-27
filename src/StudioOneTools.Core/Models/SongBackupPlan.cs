namespace StudioOneTools.Core.Models;

public sealed class SongBackupPlan
{
    public required string                             BackupRootPath   { get; init; }

    public required IReadOnlyList<SongBackupPlanFolder> Folders         { get; init; }

    public int TotalFilesToCopy => Folders.Sum(f => f.FileCount);
}
