namespace StudioOneTools.Core.Models;

public sealed class SongPathFixResult
{
    public required string SongFilePath   { get; init; }

    public required int    PathsUpdated   { get; init; }
}
