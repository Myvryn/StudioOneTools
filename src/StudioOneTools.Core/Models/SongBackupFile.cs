namespace StudioOneTools.Core.Models;

public sealed class SongBackupFile
{
    public required string SourcePath      { get; init; }

    public required string DestinationPath { get; init; }

    public required string RelativePath    { get; init; }

    public bool IsNew { get; init; }

    public bool IsUnusedAudio { get; init; }
}
