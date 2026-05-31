namespace StudioOneTools.Core.Models;

public sealed class SongRenameResult
{
    public required string                NewFolderPath    { get; init; }

    public required int                   RenamedFileCount { get; init; }

    public required int                   PatchedFileCount { get; init; }

    public required IReadOnlyList<string> Errors           { get; init; }
}
