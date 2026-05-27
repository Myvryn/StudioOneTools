namespace StudioOneTools.Core.Models;

public sealed class SongBackupResult
{
    public required int                   FilesCopied      { get; init; }

    public required int                   FoldersProcessed { get; init; }

    public required IReadOnlyList<string> Errors           { get; init; }
}
