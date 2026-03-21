using StudioOneTools.Core.Models;

namespace StudioOneTools.Core.Contracts;

public interface ISongFolderSweeper
{
    IReadOnlyList<SweepFolderResult> Sweep(string rootFolderPath);
}
