namespace StudioOneTools.Core.Models;

public sealed class SongBackupPlanFolder
{
    public required string                        SourceFolderPath      { get; init; }

    public required string                        DestinationFolderPath { get; init; }

    public required string                        FolderName            { get; init; }

    public required IReadOnlyList<SongBackupFile> FilesToCopy           { get; init; }

    public int FileCount                => FilesToCopy.Count;

    public int FileCountExcludingUnused => FilesToCopy.Count(f => !f.IsUnusedAudio);
}
