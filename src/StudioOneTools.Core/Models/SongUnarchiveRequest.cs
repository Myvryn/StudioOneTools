namespace StudioOneTools.Core.Models;

public sealed class SongUnarchiveRequest
{
    public required string ArchiveFilePath   { get; init; }

    public required string DestinationFolder { get; init; }
}
