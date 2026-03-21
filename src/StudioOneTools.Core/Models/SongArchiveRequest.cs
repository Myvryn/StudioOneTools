namespace StudioOneTools.Core.Models;

public sealed class SongArchiveRequest
{
    public required string SongFolderPath   { get; init; }

    public required string ArchiveFilePath  { get; init; }

    public string? SongFilePath             { get; init; }

    public bool IncludeUnusedMediaFiles     { get; init; } = false;

    public bool RetainMixdownFiles          { get; init; } = true;

    public bool RetainMasterFiles           { get; init; } = true;

    public bool DebugMode                   { get; init; } = false;
}
