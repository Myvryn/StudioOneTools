using StudioOneTools.Core.Models;

namespace StudioOneTools.Core.Contracts;

public interface IStudioOneSongAnalyzer
{
    IReadOnlyList<SongFileInfo> GetSongFiles(string songFolderPath);

    SongAnalysisResult Analyze(string songFolderPath, string? songFilePath = null);

    string DiscoverSongStructure(string songFilePath);
}
