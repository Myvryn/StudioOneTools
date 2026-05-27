namespace StudioOneTools.Core.Models;

public sealed class SongRenameRequest
{
    public required string FolderPath { get; init; }

    public required string OldName    { get; init; }

    public required string NewName    { get; init; }
}
