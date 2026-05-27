using StudioOneTools.Core.Models;

namespace StudioOneTools.Core.Contracts;

public interface ISongRenamer
{
    SongRenameAnalysis Analyze(string folderPath);

    Task<SongRenameResult> RenameAsync(SongRenameRequest request);
}
