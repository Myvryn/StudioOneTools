namespace StudioOneTools.Core.Models;

public sealed class SongChannel
{
    public required string Name                      { get; init; }

    public required IReadOnlyList<SongPlugin> Plugins    { get; init; }

    public required IReadOnlyList<string> MediaFiles { get; init; }
}
