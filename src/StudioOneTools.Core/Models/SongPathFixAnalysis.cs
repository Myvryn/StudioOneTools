namespace StudioOneTools.Core.Models;

public sealed class SongPathFixAnalysis
{
    public required string SongFilePath           { get; init; }

    public required string StoredSongFolderPath   { get; init; }

    public required string CurrentSongFolderPath  { get; init; }

    public required bool   NeedsFixing            { get; init; }

    public required int    PathsToUpdate          { get; init; }
}
