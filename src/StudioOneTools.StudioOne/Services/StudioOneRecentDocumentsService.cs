using System.Xml.Linq;

namespace StudioOneTools.StudioOne.Services;

/// <summary>
/// Manages the Studio One RecentDocuments.settings file so stale entries can be
/// cleaned up after songs are deleted or archived.
/// </summary>
public static class StudioOneRecentDocumentsService
{
    private static readonly string[] KnownSettingsPaths =
    [
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Fender",   "Studio Pro 8", "RecentDocuments.settings"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PreSonus", "Studio One 6", "RecentDocuments.settings"),
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "PreSonus", "Studio One 5", "RecentDocuments.settings"),
    ];

    /// <summary>
    /// Removes all recent-document entries whose paths fall under any of the given
    /// folder paths.  Returns the number of entries removed, or 0 if the settings
    /// file could not be found or modified.
    /// </summary>
    public static int RemoveSongsInFolders(IEnumerable<string> folderPaths)
    {
        var settingsPath = KnownSettingsPaths.FirstOrDefault(File.Exists);
        if (settingsPath is null)
            return 0;

        // Normalize each folder to "C:\path\to\folder\" (trailing separator) for prefix matching.
        var normalizedFolders = folderPaths
            .Select(p => Path.GetFullPath(p).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar)
            .ToList();

        if (normalizedFolders.Count == 0)
            return 0;

        var doc         = XDocument.Load(settingsPath);
        var urlElements = doc.Descendants("Url").ToList();
        var removed     = 0;

        foreach (var element in urlElements)
        {
            var url = element.Attribute("url")?.Value;
            if (url is null)
                continue;

            var localPath = UrlToLocalPath(url);
            if (localPath is null)
                continue;

            var normalizedPath = Path.GetFullPath(localPath);

            if (normalizedFolders.Any(folder => normalizedPath.StartsWith(folder, StringComparison.OrdinalIgnoreCase)))
            {
                element.Remove();
                removed++;
            }
        }

        if (removed > 0)
            doc.Save(settingsPath);

        return removed;
    }

    // Converts a "file:///C:/path/to/file.song" URL to a local Windows path.
    // Handles both encoded and unencoded URLs.
    private static string? UrlToLocalPath(string url)
    {
        const string prefix = "file:///";
        if (!url.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return null;

        var path = url.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
        return Uri.UnescapeDataString(path);
    }
}
