namespace StudioOneTools.Core.Models;

public sealed class SongRenameAnalysis
{
    public required string                   FolderPath      { get; init; }

    public required string                   CurrentName     { get; init; }

    public required bool                     IsDefaultName   { get; init; }

    public required IReadOnlyList<string>    NameSuggestions { get; init; }

    public          SongPreviewFile?         PreviewFile     { get; init; }
}
