using System.IO.Compression;
using System.Text.RegularExpressions;
using System.Xml.Linq;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;

namespace StudioOneTools.StudioOne.Services;

public sealed class StudioOneSongAnalyzer : IStudioOneSongAnalyzer
{
    #region Public Methods

    public IReadOnlyList<SongFileInfo> GetSongFiles(string songFolderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songFolderPath);

        var normalizedPath = Path.GetFullPath(songFolderPath);

        if (!Directory.Exists(normalizedPath))
        {
            return [];
        }

        return Directory.GetFiles(normalizedPath, "*.song", SearchOption.TopDirectoryOnly)
            .Select(filePath => new SongFileInfo
            {
                FilePath     = filePath,
                FileName     = Path.GetFileNameWithoutExtension(filePath),
                LastModified = File.GetLastWriteTime(filePath),
            })
            .OrderByDescending(info => info.LastModified)
            .ToArray();
    }

    public SongAnalysisResult Analyze(string songFolderPath, string? songFilePath = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songFolderPath);

        var normalizedSongFolderPath = Path.GetFullPath(songFolderPath);

        if (!Directory.Exists(normalizedSongFolderPath))
        {
            throw new DirectoryNotFoundException($"Song folder was not found: {normalizedSongFolderPath}");
        }

        var resolvedSongFilePath = songFilePath ?? GetSongFilePath(normalizedSongFolderPath);
        var mediaFolderPath = Path.Combine(normalizedSongFolderPath, "Media");
        var issues          = new List<string>();
        var mediaFiles      = new List<SongMediaFile>();

        using var songArchive = ZipFile.OpenRead(resolvedSongFilePath);

        var songDocument      = LoadRequiredXml(songArchive, "Song/song.xml");
        var mediaPoolDocument = LoadRequiredXml(songArchive, "Song/mediapool.xml");
        var usedClipIds       = GetUsedAudioClipIds(songDocument);
        var mediaPoolClips    = GetMediaPoolAudioClips(mediaPoolDocument, mediaFolderPath);
        var actualMediaFiles  = GetActualMediaFiles(mediaFolderPath);

        // Try to get plugins from song.xml, mediapool.xml, and AudioMixer.xml
        var allPluginsFromSong = GetPluginNamesFromDocument(songDocument);
        var allPluginsFromMedia = GetPluginNamesFromDocument(mediaPoolDocument);
        var allPluginsFromAudioMixer = GetPluginsFromAudioMixer(songArchive);
        var allPlugins = allPluginsFromSong.Concat(allPluginsFromMedia).Concat(allPluginsFromAudioMixer)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // Also load AudioMixer document for channel-to-plugin mapping
        XDocument audioMixerDoc = null;
        try
        {
            audioMixerDoc = LoadRequiredXml(songArchive, "Devices/audiomixer.xml");
        }
        catch
        {
            // AudioMixer.xml might not exist
        }

        foreach (var mediaPoolClip in mediaPoolClips.Values.OrderBy(clip => clip.RelativePath, StringComparer.OrdinalIgnoreCase))
        {
            var existsOnDisk = actualMediaFiles.ContainsKey(mediaPoolClip.RelativePath);
            var isUsed       = usedClipIds.Contains(mediaPoolClip.MediaId) || mediaPoolClip.UseCount > 0;
            var fullPath     = existsOnDisk
                ? actualMediaFiles[mediaPoolClip.RelativePath]
                : Path.Combine(mediaFolderPath, mediaPoolClip.RelativePath);

            if (isUsed && !existsOnDisk)
            {
                issues.Add($"Referenced media file is missing: {mediaPoolClip.RelativePath}");
            }

            mediaFiles.Add(new SongMediaFile
            {
                FileName      = Path.GetFileName(mediaPoolClip.RelativePath),
                RelativePath  = mediaPoolClip.RelativePath,
                FullPath      = fullPath,
                SourceMediaId = mediaPoolClip.MediaId,
                UseCount      = mediaPoolClip.UseCount,
                IsUsed        = isUsed,
                ExistsOnDisk  = existsOnDisk,
                IsWaveFile    = IsWaveFile(mediaPoolClip.RelativePath),
            });
        }

        foreach (var actualMediaFile in actualMediaFiles.OrderBy(pair => pair.Key, StringComparer.OrdinalIgnoreCase))
        {
            if (mediaPoolClips.ContainsKey(actualMediaFile.Key))
            {
                continue;
            }

            mediaFiles.Add(new SongMediaFile
            {
                FileName      = Path.GetFileName(actualMediaFile.Key),
                RelativePath  = actualMediaFile.Key,
                FullPath      = actualMediaFile.Value,
                SourceMediaId = null,
                UseCount      = 0,
                IsUsed        = false,
                ExistsOnDisk  = true,
                IsWaveFile    = IsWaveFile(actualMediaFile.Key),
            });
        }

        return new SongAnalysisResult
        {
            SongName        = GetSongName(resolvedSongFilePath),
            SongFolderPath  = normalizedSongFolderPath,
            SongFilePath    = resolvedSongFilePath,
            MediaFolderPath = mediaFolderPath,
            MediaFiles      = mediaFiles.OrderBy(file => file.RelativePath, StringComparer.OrdinalIgnoreCase).ToArray(),
            Issues          = issues.OrderBy(message => message, StringComparer.OrdinalIgnoreCase).ToArray(),
            Plugins         = allPlugins.Select(ParsePluginName).OrderBy(p => p.DisplayName).Select(p => p.DisplayName).Distinct().ToArray(),
            Channels        = GetChannels(songDocument, mediaPoolClips, allPlugins, audioMixerDoc),
            PreviewFile     = GetPreviewFile(normalizedSongFolderPath),
        };
    }

    /// <summary>
    /// Opens a .song file and produces a human-readable report of every XML element and
    /// attribute found inside song.xml.  Use this to discover what data is available in
    /// a specific Studio One version's files.
    /// </summary>
    public string DiscoverSongStructure(string songFilePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(songFilePath);

        using var archive     = ZipFile.OpenRead(songFilePath);
        var       songDoc     = LoadRequiredXml(archive, "Song/song.xml");
        var       sb          = new System.Text.StringBuilder();
        var       songRoot    = songDoc.Root;

        if (songRoot is null)
        {
            return "song.xml has no root element.";
        }

        // ── Song-root attributes ────────────────────────────────────────────
        sb.AppendLine("=== Song root element ===");
        sb.AppendLine($"  <{songRoot.Name.LocalName}>");

        foreach (var attr in songRoot.Attributes())
        {
            sb.AppendLine($"    @{attr.Name.LocalName} = \"{attr.Value}\"");
        }

        // ── Unique element catalogue ─────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("=== All unique element types (with sample attributes) ===");

        var elementGroups = songDoc
            .Descendants()
            .GroupBy(e => e.Name.LocalName, StringComparer.OrdinalIgnoreCase)
            .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

        foreach (var group in elementGroups)
        {
            // Collect every attribute name seen across all instances of this element type
            var allAttrs = group
                .SelectMany(e => e.Attributes())
                .GroupBy(a => a.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g2 => g2.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g2 => (Name: g2.Key, Sample: g2.First().Value))
                .ToArray();

            var count = group.Count();
            sb.AppendLine($"  <{group.Key}> ({count} instance{(count == 1 ? "" : "s")})");

            foreach (var (name, sample) in allAttrs)
            {
                var display = sample.Length > 60 ? sample[..60] + "…" : sample;
                sb.AppendLine($"    @{name} = \"{display}\"");
            }
        }

        // ── Track-specific deep-dive ──────────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("=== Track elements (deep-dive) ===");

        var trackElements = songDoc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "Track", StringComparison.OrdinalIgnoreCase) ||
                        e.Name.LocalName.EndsWith("Track", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (trackElements.Length == 0)
        {
            sb.AppendLine("  (no track elements found)");
        }

        foreach (var track in trackElements)
        {
            var trackName = (string?)track.Attribute("name") ?? "(unnamed)";
            sb.AppendLine($"  Track \"{trackName}\" <{track.Name.LocalName}>");

            foreach (var attr in track.Attributes())
            {
                var display = attr.Value.Length > 80 ? attr.Value[..80] + "…" : attr.Value;
                sb.AppendLine($"    @{attr.Name.LocalName} = \"{display}\"");
            }

            // Direct children summary
            var childGroups = track.Elements()
                .GroupBy(c => c.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            foreach (var cg in childGroups)
            {
                sb.AppendLine($"    child <{cg.Key}> ×{cg.Count()}");
            }
        }

        // ── AudioDevice elements location ──────────────────────────────────────
        sb.AppendLine();
        sb.AppendLine("=== AudioDevice elements (hierarchy) ===");

        var audioDevices = songDoc.Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "AudioDevice", StringComparison.OrdinalIgnoreCase))
            .ToArray();

        if (audioDevices.Length == 0)
        {
            sb.AppendLine("  (no AudioDevice elements found in song.xml)");
            sb.AppendLine();
            sb.AppendLine("  NOTE: AudioDevice elements might be stored in mediapool.xml or another file.");
            sb.AppendLine("  Searching alternative locations...");

            try
            {
                using var archive2 = ZipFile.OpenRead(songFilePath);
                var mediapoolDoc = LoadRequiredXml(archive2, "Song/mediapool.xml");
                var mediapoolDevices = mediapoolDoc.Descendants()
                    .Where(e => string.Equals(e.Name.LocalName, "AudioDevice", StringComparison.OrdinalIgnoreCase))
                    .ToArray();

                sb.AppendLine();
                if (mediapoolDevices.Length > 0)
                {
                    sb.AppendLine($"  Found {mediapoolDevices.Length} AudioDevice(s) in mediapool.xml!");
                    foreach (var device in mediapoolDevices.Take(5))
                    {
                        var deviceName = (string?)device.Attribute("deviceName") ?? "(no name)";
                        sb.AppendLine($"    Device: {deviceName}");
                    }
                }
                else
                {
                    sb.AppendLine("  No AudioDevice elements found in mediapool.xml either.");
                    sb.AppendLine("  Plugins may be stored under a different element name or structure.");
                    sb.AppendLine("  Searching for other plugin-related elements...");

                    // Look for AudioEffectClip elements (effects/plugins)
                    var effectClips = mediapoolDoc.Descendants()
                        .Where(e => string.Equals(e.Name.LocalName, "AudioEffectClip", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    if (effectClips.Length > 0)
                    {
                        sb.AppendLine();
                        sb.AppendLine($"  Found {effectClips.Length} AudioEffectClip element(s) in mediapool.xml!");
                        sb.AppendLine("  === DETAILED PLUGIN LIST ===");

                        // Show ALL AudioEffectClipItems across all AudioEffectClips
                        var allEffectItems = mediapoolDoc.Descendants()
                            .Where(e => string.Equals(e.Name.LocalName, "AudioEffectClipItem", StringComparison.OrdinalIgnoreCase))
                            .ToArray();

                        sb.AppendLine($"  Total AudioEffectClipItem(s) found: {allEffectItems.Length}");
                        sb.AppendLine();

                        foreach (var item in allEffectItems)
                        {
                            var name = (string?)item.Attribute("name") ?? "(unnamed)";
                            var classId = (string?)item.Attribute("classID") ?? "(no classID)";
                            sb.AppendLine($"    Plugin: {name} [classID: {classId}]");
                        }

                        sb.AppendLine();
                        sb.AppendLine("  === AudioEffectClip Structure ===");
                        foreach (var clip in effectClips.Take(5))
                        {
                            sb.AppendLine($"    AudioEffectClip attributes: {string.Join(", ", clip.Attributes().Select(a => $"{a.Name.LocalName}=\"{a.Value}\""))}");

                            // Show child elements
                            var children = clip.Elements().GroupBy(e => e.Name.LocalName, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key);
                            foreach (var childGroup in children)
                            {
                                sb.AppendLine($"      child <{childGroup.Key}> ×{childGroup.Count()}");
                            }
                        }
                    }
                    else
                    {
                        // List all unique element types in mediapool.xml with counts
                        var allElements = mediapoolDoc.Descendants()
                            .GroupBy(e => e.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                            .OrderBy(g => g.Key)
                            .Select(g => new { Element = g.Key, Count = g.Count() })
                            .ToArray();

                        sb.AppendLine("  All unique element types in mediapool.xml:");
                        foreach (var elem in allElements)
                        {
                            sb.AppendLine($"    <{elem.Element}> ×{elem.Count}");
                        }
                    }
                }

                // Check AudioMixer.xml for plugins
                sb.AppendLine();
                sb.AppendLine("=== AudioMixer.xml Plugin Scan ===");
                try
                {
                    using var archive3 = ZipFile.OpenRead(songFilePath);
                    var audioMixerDoc = LoadRequiredXml(archive3, "Devices/audiomixer.xml");

                    // Show channel structure for plugin discovery
                    sb.AppendLine("  Channel-to-Plugin Mapping:");
                    var audioTrackChannels = audioMixerDoc.Descendants()
                        .Where(e => string.Equals(e.Name.LocalName, "AudioTrackChannel", StringComparison.OrdinalIgnoreCase))
                        .ToArray();

                    foreach (var channel in audioTrackChannels.Take(5))
                    {
                        var channelLabel = (string?)channel.Attribute("label") ?? "(no label)";
                        var channelName = (string?)channel.Attribute("name") ?? "(no name)";
                        sb.AppendLine($"    Channel: label=\"{channelLabel}\" name=\"{channelName}\"");

                        // Find all plugin-related attributes
                        var allChildren = channel.Elements()
                            .GroupBy(e => e.Name.LocalName, StringComparer.OrdinalIgnoreCase)
                            .OrderBy(g => g.Key);

                        foreach (var childGroup in allChildren)
                        {
                            sb.AppendLine($"      child <{childGroup.Key}> ×{childGroup.Count()}");

                            // If it's Attributes, show id and name attributes
                            if (string.Equals(childGroup.Key, "Attributes", StringComparison.OrdinalIgnoreCase))
                            {
                                foreach (var attr in childGroup)
                                {
                                    var id = (string?)attr.Attribute("id") ?? "(no id)";
                                    var name = (string?)attr.Attribute("name") ?? "(no name)";
                                    var pname = (string?)attr.Attribute("pname") ?? "(no pname)";
                                    sb.AppendLine($"        id=\"{id}\" name=\"{name}\" pname=\"{pname}\"");

                                    // Look for nested deviceData
                                    var deviceData = attr.Descendants()
                                        .FirstOrDefault(e => string.Equals(e.Name.LocalName, "Attributes", StringComparison.OrdinalIgnoreCase) &&
                                                            string.Equals((string?)e.Attribute("id"), "deviceData", StringComparison.OrdinalIgnoreCase));
                                    if (deviceData != null)
                                    {
                                        var deviceName = (string?)deviceData.Attribute("name") ?? "(no name)";
                                        sb.AppendLine($"          -> deviceData: {deviceName}");
                                    }
                                }
                            }
                        }
                    }

                    // Also look for Attributes elements with pname attribute
                    var attributesWithPname = audioMixerDoc
                        .Descendants()
                        .Where(e => string.Equals(e.Name.LocalName, "Attributes", StringComparison.OrdinalIgnoreCase))
                        .Where(e => e.Attribute("pname") != null)
                        .ToArray();

                    sb.AppendLine();
                    sb.AppendLine($"  Total Attributes elements with 'pname' attribute: {attributesWithPname.Length}");
                }
                catch (Exception ex)
                {
                    sb.AppendLine($"  Error reading AudioMixer.xml: {ex.Message}");
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine($"  Error searching mediapool.xml: {ex.Message}");
            }
        }
        else
        {
            sb.AppendLine($"  Found {audioDevices.Length} AudioDevice element(s):");
            foreach (var device in audioDevices.Take(10))
            {
                var deviceName = (string?)device.Attribute("deviceName") ?? "(no name)";
                sb.AppendLine($"    Device: {deviceName}");

                // Show parent hierarchy
                var parent = device.Parent;
                var depth = 1;
                while (parent is not null && depth <= 5)
                {
                    var parentName = (string?)parent.Attribute("name") ?? parent.Name.LocalName;
                    sb.AppendLine($"      > Parent: <{parent.Name.LocalName}> name=\"{parentName}\"");
                    parent = parent.Parent;
                    depth++;
                }
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    #endregion

    #region Private Methods

    private static string GetSongFilePath(string songFolderPath)
    {
        var songFiles = Directory.GetFiles(songFolderPath, "*.song", SearchOption.TopDirectoryOnly);

        return songFiles.Length switch
        {
            0 => throw new FileNotFoundException($"No .song file was found in {songFolderPath}."),
            1 => songFiles[0],
            _ => songFiles.OrderByDescending(File.GetLastWriteTime).First(),
        };
    }

    private static XDocument LoadRequiredXml(ZipArchive archive, string entryPath)
    {
        var entry = archive.GetEntry(entryPath)
            ?? throw new InvalidDataException($"The song archive does not contain required entry '{entryPath}'.");

        using var stream  = entry.Open();
        using var reader  = new StreamReader(stream);
        var       xmlContent = reader.ReadToEnd();

        try
        {
            return XDocument.Parse(xmlContent, LoadOptions.None);
        }
        catch (System.Xml.XmlException)
        {
            // Studio One XML files may reference namespace prefixes (e.g. "x:") without declaring them.
            // Strip undeclared prefixes so the document can be parsed; the code only uses LocalName.
            return XDocument.Parse(StripNamespacePrefixes(xmlContent), LoadOptions.None);
        }
    }

    private static string StripNamespacePrefixes(string xml)
    {
        // Remove C++ scope resolution operators: ProcessGraph::SlotNode -> ProcessGraphSlotNode
        xml = Regex.Replace(xml, "::", "");

        // Remove XML namespace prefixes: prefix:name -> name
        xml = Regex.Replace(xml, @"\w+:(\w+)", "$1");

        // Remove any remaining colons in element names (between < and > or whitespace)
        // This handles edge cases like <:name> or lingering prefixes
        xml = Regex.Replace(xml, @"([<\s]):", "$1");      // Remove colon after < or whitespace
        xml = Regex.Replace(xml, @":([>\s])", "$1");      // Remove colon before > or whitespace

        return xml;
    }

    private static HashSet<string> GetUsedAudioClipIds(XDocument songDocument)
    {
        return songDocument
            .Descendants()
            .Where(element => string.Equals(element.Name.LocalName, "AudioEvent", StringComparison.Ordinal))
            .Select(element => (string?)element.Attribute("clipID"))
            .Where(clipId => !string.IsNullOrWhiteSpace(clipId))
            .Cast<string>()
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    private static Dictionary<string, MediaPoolAudioClip> GetMediaPoolAudioClips(XDocument mediaPoolDocument, string mediaFolderPath)
    {
        var clips = new Dictionary<string, MediaPoolAudioClip>(StringComparer.OrdinalIgnoreCase);

        foreach (var audioClipElement in mediaPoolDocument.Descendants().Where(element => string.Equals(element.Name.LocalName, "AudioClip", StringComparison.Ordinal)))
        {
            var mediaId = (string?)audioClipElement.Attribute("mediaID");

            if (string.IsNullOrWhiteSpace(mediaId))
            {
                continue;
            }

            var pathUrl = GetPathUrl(audioClipElement);

            if (string.IsNullOrWhiteSpace(pathUrl))
            {
                continue;
            }

            if (!TryGetRelativeMediaPath(pathUrl, mediaFolderPath, out var relativePath))
            {
                continue;
            }

            var useCount = (int?)audioClipElement.Attribute("useCount") ?? 0;

            clips[NormalizeRelativePath(relativePath)] = new MediaPoolAudioClip(mediaId, NormalizeRelativePath(relativePath), useCount);
        }

        return clips;
    }

    private static string? GetPathUrl(XElement audioClipElement)
    {
        foreach (var urlElement in audioClipElement.Descendants().Where(element => string.Equals(element.Name.LocalName, "Url", StringComparison.Ordinal)))
        {
            var idAttribute = urlElement
                .Attributes()
                .FirstOrDefault(attribute => string.Equals(attribute.Name.LocalName, "id", StringComparison.Ordinal) &&
                                             string.Equals(attribute.Value, "path", StringComparison.Ordinal));

            if (idAttribute is null)
            {
                continue;
            }

            return (string?)urlElement.Attribute("url");
        }

        return null;
    }

    private static bool TryGetRelativeMediaPath(string pathUrl, string mediaFolderPath, out string relativePath)
    {
        relativePath = string.Empty;

        if (!Uri.TryCreate(pathUrl, UriKind.Absolute, out var uri) || !uri.IsFile)
        {
            return false;
        }

        var localPath           = uri.LocalPath;
        var fullMediaPath       = Path.GetFullPath(mediaFolderPath);
        var normalizedMediaPath = fullMediaPath.EndsWith(Path.DirectorySeparatorChar)
            ? fullMediaPath
            : fullMediaPath + Path.DirectorySeparatorChar;

        if (!localPath.StartsWith(normalizedMediaPath, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        relativePath = NormalizeRelativePath(Path.GetRelativePath(fullMediaPath, localPath));

        return !string.IsNullOrWhiteSpace(relativePath);
    }

    private static Dictionary<string, string> GetActualMediaFiles(string mediaFolderPath)
    {
        var actualMediaFiles = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (!Directory.Exists(mediaFolderPath))
        {
            return actualMediaFiles;
        }

        foreach (var filePath in Directory.GetFiles(mediaFolderPath, "*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(mediaFolderPath, filePath);

            actualMediaFiles[NormalizeRelativePath(relativePath)] = filePath;
        }

        return actualMediaFiles;
    }

    private static string NormalizeRelativePath(string relativePath)
    {
        return relativePath.Replace('/', '\\');
    }

    private static bool IsWaveFile(string relativePath)
    {
        return string.Equals(Path.GetExtension(relativePath), ".wav", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetSongName(string songFilePath)
    {
        return Path.GetFileNameWithoutExtension(songFilePath);
    }

    private static IReadOnlyList<string> GetPluginNames(XDocument songDocument)
    {
        var plugins = songDocument
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "AudioDevice", StringComparison.Ordinal))
            .Select(e => (string?)e.Attribute("deviceName"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>()
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        // If no plugins found in song.xml, they might be in mediapool.xml
        if (plugins.Length == 0)
        {
            // Note: mediapool is loaded separately in Analyze method
            // For now, return empty - actual extraction happens in GetChannels
        }

        return plugins
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetPluginNamesFromDocument(XDocument document)
    {
        var plugins = new List<string>();

        // Get plugins from AudioDevice elements (song.xml)
        plugins.AddRange(document
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "AudioDevice", StringComparison.OrdinalIgnoreCase))
            .Select(e => (string?)e.Attribute("deviceName"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>());

        // Get plugins from AudioEffectClipItem elements (mediapool.xml)
        plugins.AddRange(document
            .Descendants()
            .Where(e => string.Equals(e.Name.LocalName, "AudioEffectClipItem", StringComparison.OrdinalIgnoreCase))
            .Select(e => (string?)e.Attribute("name"))
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Cast<string>());

        return plugins
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    private static IReadOnlyList<string> GetPluginsFromAudioMixer(ZipArchive songArchive)
    {
        try
        {
            var audioMixerDoc = LoadRequiredXml(songArchive, "Devices/audiomixer.xml");

            // Extract plugin names from Attributes elements with pname attribute
            var plugins = audioMixerDoc
                .Descendants()
                .Where(e => string.Equals(e.Name.LocalName, "Attributes", StringComparison.OrdinalIgnoreCase))
                .Select(e => (string?)e.Attribute("pname"))
                .Where(name => !string.IsNullOrWhiteSpace(name) && !string.Equals(name, "default", StringComparison.OrdinalIgnoreCase))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            return plugins;
        }
        catch
        {
            // AudioMixer.xml might not exist in all songs
            return Array.Empty<string>();
        }
    }

    private static SongPreviewFile? GetPreviewFile(string songFolderPath)
    {
        var audioExtensions = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            { ".mp3", ".wav", ".aif", ".aiff", ".flac", ".m4a", ".ogg" };

        SongPreviewFile? latest      = null;
        var             latestTime   = DateTime.MinValue;

        foreach (var (subFolder, label) in new[] { ("Master", "Master"), ("Mixdown", "Mixdown") })
        {
            var folderPath = Path.Combine(songFolderPath, subFolder);

            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (var filePath in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                if (!audioExtensions.Contains(Path.GetExtension(filePath)))
                {
                    continue;
                }

                var writeTime = File.GetLastWriteTime(filePath);

                if (writeTime <= latestTime)
                {
                    continue;
                }

                latestTime = writeTime;
                latest = new SongPreviewFile
                {
                    FilePath = filePath,
                    FileName = Path.GetFileName(filePath),
                    FileType = label,
                };
            }
        }

        return latest;
    }

    private static SongPlugin ParsePluginName(string deviceName)
    {
        // Strip version numbers: "StudioRack Stereo 4" -> "StudioRack Stereo"
        var normalizedName = Regex.Replace(deviceName, @"\s+\d+$", string.Empty);

        // deviceName is typically "Vendor/PluginName" or just "PluginName" for built-ins
        var parts = normalizedName.Split('/', StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length == 2)
        {
            // "Vendor/PluginName" format
            return new SongPlugin
            {
                Vendor = parts[0],
                Name   = parts[1],
            };
        }

        // Single-part name (built-in plugin)
        return new SongPlugin
        {
            Vendor = string.Empty,
            Name   = normalizedName,
        };
    }

    private static IReadOnlyList<SongChannel> GetChannels(
        XDocument songDocument,
        Dictionary<string, MediaPoolAudioClip> mediaPoolClips,
        string[] allPlugins,
        XDocument? audioMixerDoc)
    {
        // Build reverse lookup: clipID → display file name
        var clipIdToFileName = mediaPoolClips.Values
            .ToDictionary(
                clip => clip.MediaId,
                clip => Path.GetFileName(clip.RelativePath),
                StringComparer.OrdinalIgnoreCase);

        // Build channel label to plugins mapping from AudioMixer
        var channelToPlugins = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        if (audioMixerDoc != null)
        {
            // Process both AudioTrackChannel and AudioSynthChannel elements
            var allChannels = audioMixerDoc.Descendants()
                .Where(e => (string.Equals(e.Name.LocalName, "AudioTrackChannel", StringComparison.OrdinalIgnoreCase) ||
                            string.Equals(e.Name.LocalName, "AudioSynthChannel", StringComparison.OrdinalIgnoreCase)))
                .ToArray();

            foreach (var channel in allChannels)
            {
                var channelLabel = (string?)channel.Attribute("label");
                if (string.IsNullOrWhiteSpace(channelLabel))
                {
                    continue;
                }

                var plugins = new List<string>();

                // Find all FX plugin slots in this channel's Inserts
                var insertsElement = channel.Descendants()
                    .FirstOrDefault(e => string.Equals(e.Name.LocalName, "Attributes", StringComparison.OrdinalIgnoreCase) &&
                                        string.Equals((string?)e.Attribute("id"), "Inserts", StringComparison.OrdinalIgnoreCase));

                if (insertsElement != null)
                {
                    // Find all FX01, FX02, etc. plugin slots
                    foreach (var fxSlot in insertsElement.Elements()
                        .Where(e => string.Equals(e.Name.LocalName, "Attributes", StringComparison.OrdinalIgnoreCase) &&
                                   ((string?)e.Attribute("name") ?? "").StartsWith("FX", StringComparison.OrdinalIgnoreCase)))
                    {
                        // First, try to get plugin name from deviceData element
                        var deviceData = fxSlot.Descendants()
                            .FirstOrDefault(e => string.Equals(e.Name.LocalName, "Attributes", StringComparison.OrdinalIgnoreCase) &&
                                                string.Equals((string?)e.Attribute("id"), "deviceData", StringComparison.OrdinalIgnoreCase));

                        if (deviceData != null)
                        {
                            var pluginName = (string?)deviceData.Attribute("name");
                            if (!string.IsNullOrWhiteSpace(pluginName))
                            {
                                plugins.Add(pluginName);
                            }
                        }

                        // Also check for plugins stored in Presets Attributes with pname
                        var presetsWithPname = fxSlot.Elements()
                            .Where(e => string.Equals(e.Name.LocalName, "Attributes", StringComparison.OrdinalIgnoreCase) &&
                                       string.Equals((string?)e.Attribute("id"), "Presets", StringComparison.OrdinalIgnoreCase) &&
                                       e.Attribute("pname") != null)
                            .ToArray();

                        foreach (var preset in presetsWithPname)
                        {
                            var pluginName = (string?)preset.Attribute("pname");
                            if (!string.IsNullOrWhiteSpace(pluginName) && 
                                !string.Equals(pluginName, "default", StringComparison.OrdinalIgnoreCase) &&
                                !plugins.Contains(pluginName, StringComparer.OrdinalIgnoreCase))
                            {
                                plugins.Add(pluginName);
                            }
                        }
                    }
                }

                if (plugins.Count > 0)
                {
                    channelToPlugins[channelLabel] = plugins;
                }
            }
        }

        var channels = new List<SongChannel>();

        // Find track elements: LocalName is "Track" or ends with "Track" (AudioTrack, InstrumentTrack, etc.)
        foreach (var trackElement in songDocument.Descendants()
            .Where(e => (string.Equals(e.Name.LocalName, "Track", StringComparison.OrdinalIgnoreCase) ||
                         e.Name.LocalName.EndsWith("Track", StringComparison.OrdinalIgnoreCase)) &&
                        !string.IsNullOrWhiteSpace((string?)e.Attribute("name"))))
        {
            var name = (string?)trackElement.Attribute("name") ?? string.Empty;

            var mediaFiles = trackElement.Descendants()
                .Where(d => string.Equals(d.Name.LocalName, "AudioEvent", StringComparison.OrdinalIgnoreCase))
                .Select(d => (string?)d.Attribute("clipID"))
                .Where(id => !string.IsNullOrWhiteSpace(id))
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Select(id => clipIdToFileName.TryGetValue(id, out var file) ? file : null)
                .Where(f => f is not null)
                .Cast<string>()
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Order(StringComparer.OrdinalIgnoreCase)
                .ToArray();

            // Get plugins for this channel if they exist in AudioMixer
            var channelPlugins = Array.Empty<SongPlugin>();
            if (channelToPlugins.TryGetValue(name, out var pluginNames))
            {
                channelPlugins = pluginNames
                    .Select(ParsePluginName)
                    .DistinctBy(p => p.DisplayName, StringComparer.OrdinalIgnoreCase)
                    .OrderBy(p => p.DisplayName)
                    .ToArray();
            }

            channels.Add(new SongChannel
            {
                Name       = name,
                Plugins    = channelPlugins,
                MediaFiles = mediaFiles,
            });
        }

        return channels
            .DistinctBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c.Name, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    #endregion

    #region Private Types

    private sealed record MediaPoolAudioClip(string MediaId, string RelativePath, int UseCount);

    #endregion
}
