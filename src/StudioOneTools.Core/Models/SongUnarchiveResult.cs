namespace StudioOneTools.Core.Models;

public sealed class SongUnarchiveResult
{
    public required string SongFolderPath { get; init; }

    public required string SongFilePath   { get; init; }

    public required int    PathsFixed     { get; init; }
}
