using StudioOneTools.Core.Models;

namespace StudioOneTools.Core.Contracts;

public interface ISongFolderUnarchiver
{
    SongUnarchiveResult Unarchive(SongUnarchiveRequest request);
}
