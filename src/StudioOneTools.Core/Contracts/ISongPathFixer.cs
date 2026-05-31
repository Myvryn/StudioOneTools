using StudioOneTools.Core.Models;

namespace StudioOneTools.Core.Contracts;

public interface ISongPathFixer
{
    SongPathFixAnalysis Analyze(string songFilePath);

    SongPathFixResult FixPaths(string songFilePath);
}
