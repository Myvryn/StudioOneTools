using StudioOneTools.Core.Models;

namespace StudioOneTools.Core.Contracts;

public interface ISongFolderArchiver
{
    SongArchiveResult CreateArchive(SongArchiveRequest request);
}
