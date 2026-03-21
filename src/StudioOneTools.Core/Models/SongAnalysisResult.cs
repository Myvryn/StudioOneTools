namespace StudioOneTools.Core.Models;

public sealed class SongAnalysisResult
{
    public required string SongName             { get; init; }

    public required string SongFolderPath       { get; init; }

    public required string SongFilePath         { get; init; }

    public required string MediaFolderPath      { get; init; }

    public required IReadOnlyList<SongMediaFile> MediaFiles { get; init; }

    public required IReadOnlyList<string> Issues { get; init; }

    public required IReadOnlyList<string> Plugins { get; init; }

    public required IReadOnlyList<SongChannel> Channels { get; init; }

    public SongPreviewFile? PreviewFile         { get; init; }

    public IReadOnlyList<SongMediaFile> UsedWaveFiles =>
        MediaFiles
            .Where(file => file.IsWaveFile && file.IsUsed)
            .ToArray();

    public IReadOnlyList<SongMediaFile> UnusedWaveFiles =>
        MediaFiles
            .Where(file => file.IsWaveFile && !file.IsUsed)
            .ToArray();

    public IReadOnlyList<SongMediaFile> MissingReferencedFiles =>
        MediaFiles
            .Where(file => file.IsWaveFile && file.IsUsed && !file.ExistsOnDisk)
            .ToArray();

    public bool HasMissingReferencedFiles => MissingReferencedFiles.Count > 0;
}
