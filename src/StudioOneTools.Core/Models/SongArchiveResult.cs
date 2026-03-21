namespace StudioOneTools.Core.Models;

public sealed class SongArchiveResult
{
    public required string ArchiveFilePath      { get; init; }

    public required SongAnalysisResult Analysis { get; init; }

    public required IReadOnlyList<string> IncludedEntries { get; init; }

    public required IReadOnlyList<string> ExcludedEntries { get; init; }
}
