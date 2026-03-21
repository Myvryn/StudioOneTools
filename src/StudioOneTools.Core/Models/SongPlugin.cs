namespace StudioOneTools.Core.Models;

public sealed class SongPlugin
{
    public required string Vendor { get; init; }

    public required string Name   { get; init; }

    public string DisplayName => string.IsNullOrWhiteSpace(Vendor)
        ? Name
        : $"{Vendor} - {Name}";
}
