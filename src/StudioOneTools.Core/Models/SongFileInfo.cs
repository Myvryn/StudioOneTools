namespace StudioOneTools.Core.Models;

public sealed class SongFileInfo
{
    public required string   FilePath     { get; init; }

    public required string   FileName     { get; init; }

    public required DateTime LastModified { get; init; }
}
