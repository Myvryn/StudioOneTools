using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;

namespace StudioOneTools.StudioOne.Services;

public sealed class SongRenamer : ISongRenamer
{
    #region Fields

    private static readonly Regex DefaultNameRegex = new(
        @"_\d{4}-\d{2}-\d{2}(_\d+)?$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private static readonly HashSet<string> TextEntryExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".xml", ".txt", ".json", ".html" };

    private static readonly HashSet<string> AudioExtensions = new(StringComparer.OrdinalIgnoreCase)
        { ".mp3", ".wav", ".aif", ".aiff", ".flac", ".m4a", ".ogg" };

    #endregion

    #region Public Methods

    public SongRenameAnalysis Analyze(string folderPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(folderPath);

        var normalizedPath = Path.GetFullPath(folderPath);

        if (!Directory.Exists(normalizedPath))
        {
            throw new DirectoryNotFoundException($"Song folder was not found: {normalizedPath}");
        }

        var currentName   = new DirectoryInfo(normalizedPath).Name;
        var isDefaultName = DefaultNameRegex.IsMatch(currentName);

        var suggestions = Directory
            .GetFiles(normalizedPath, "*.song", SearchOption.TopDirectoryOnly)
            .Select(f => Path.GetFileNameWithoutExtension(f)!)
            .Where(name => !string.IsNullOrWhiteSpace(name) &&
                           !DefaultNameRegex.IsMatch(name) &&
                           !string.Equals(name, currentName, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Order(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new SongRenameAnalysis
        {
            FolderPath      = normalizedPath,
            CurrentName     = currentName,
            IsDefaultName   = isDefaultName,
            NameSuggestions = suggestions,
            PreviewFile     = FindPreviewFile(normalizedPath),
        };
    }

    public async Task<SongRenameResult> RenameAsync(SongRenameRequest request)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(request.FolderPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.OldName);
        ArgumentException.ThrowIfNullOrWhiteSpace(request.NewName);

        var folderPath   = Path.GetFullPath(request.FolderPath);
        var oldName      = request.OldName;
        var newName      = request.NewName;
        var errors       = new List<string>();
        var renamedCount = 0;
        var patchedCount = 0;

        if (!Directory.Exists(folderPath))
        {
            throw new DirectoryNotFoundException($"Song folder was not found: {folderPath}");
        }

        // 1. Patch XML content inside each .song ZIP archive.
        var songFiles = Directory.GetFiles(folderPath, "*.song", SearchOption.TopDirectoryOnly);

        foreach (var songFile in songFiles)
        {
            try
            {
                if (await PatchZipArchiveAsync(songFile, oldName, newName))
                {
                    patchedCount++;
                }
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to patch {Path.GetFileName(songFile)}: {ex.Message}");
            }
        }

        // 2. Rename .song files whose stem matches oldName.
        foreach (var songFile in songFiles)
        {
            if (!string.Equals(Path.GetFileNameWithoutExtension(songFile), oldName, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var newSongPath = Path.Combine(folderPath, newName + ".song");

            try
            {
                File.Move(songFile, newSongPath);
                renamedCount++;
            }
            catch (Exception ex)
            {
                errors.Add($"Failed to rename {Path.GetFileName(songFile)}: {ex.Message}");
            }
        }

        // 3. Rename Mixdown/Master audio files that start with oldName.
        foreach (var subDir in new[] { "Mixdown", "Master" })
        {
            var subDirPath = Path.Combine(folderPath, subDir);

            if (!Directory.Exists(subDirPath))
            {
                continue;
            }

            foreach (var audioFile in Directory.GetFiles(subDirPath, "*", SearchOption.AllDirectories))
            {
                var fileName = Path.GetFileName(audioFile);

                if (!fileName.StartsWith(oldName, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var newFileName = newName + fileName[oldName.Length..];
                var newFilePath = Path.Combine(Path.GetDirectoryName(audioFile)!, newFileName);

                try
                {
                    File.Move(audioFile, newFilePath);
                    renamedCount++;
                }
                catch (Exception ex)
                {
                    errors.Add($"Failed to rename {fileName}: {ex.Message}");
                }
            }
        }

        // 4. Rename the folder itself (last, so relative paths above remain valid).
        var parentPath    = Path.GetDirectoryName(folderPath)!;
        var newFolderPath = Path.Combine(parentPath, newName);

        try
        {
            Directory.Move(folderPath, newFolderPath);
        }
        catch (Exception ex)
        {
            errors.Add($"Failed to rename folder: {ex.Message}");
            newFolderPath = folderPath;
        }

        return new SongRenameResult
        {
            NewFolderPath    = newFolderPath,
            RenamedFileCount = renamedCount,
            PatchedFileCount = patchedCount,
            Errors           = errors,
        };
    }

    #endregion

    #region Private Methods

    private static async Task<bool> PatchZipArchiveAsync(string zipPath, string oldName, string newName)
    {
        var tempPath = zipPath + ".renametmp";

        try
        {
            var didPatch = false;

            using (var original = ZipFile.OpenRead(zipPath))
            using (var updated  = ZipFile.Open(tempPath, ZipArchiveMode.Create))
            {
                foreach (var entry in original.Entries)
                {
                    var newEntry    = updated.CreateEntry(entry.FullName, CompressionLevel.Optimal);
                    var isTextEntry = TextEntryExtensions.Contains(Path.GetExtension(entry.FullName));

                    if (isTextEntry)
                    {
                        using var reader  = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false);
                        var       content = await reader.ReadToEndAsync();

                        if (content.Contains(oldName, StringComparison.OrdinalIgnoreCase))
                        {
                            content  = Regex.Replace(content, Regex.Escape(oldName), newName, RegexOptions.IgnoreCase);
                            didPatch = true;
                        }

                        using var writer = new StreamWriter(newEntry.Open(), Encoding.UTF8);
                        await writer.WriteAsync(content);
                    }
                    else
                    {
                        using var srcStream = entry.Open();
                        using var dstStream = newEntry.Open();
                        await srcStream.CopyToAsync(dstStream);
                    }
                }
            }

            File.Move(tempPath, zipPath, overwrite: true);
            return didPatch;
        }
        catch
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }

            throw;
        }
    }

    private static SongPreviewFile? FindPreviewFile(string songFolderPath)
    {
        SongPreviewFile? latest    = null;
        var             latestTime = DateTime.MinValue;

        foreach (var (subFolder, label) in new[] { ("Master", "Master"), ("Mixdown", "Mixdown") })
        {
            var folderPath = Path.Combine(songFolderPath, subFolder);

            if (!Directory.Exists(folderPath))
            {
                continue;
            }

            foreach (var filePath in Directory.GetFiles(folderPath, "*", SearchOption.AllDirectories))
            {
                if (!AudioExtensions.Contains(Path.GetExtension(filePath)))
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

    #endregion
}
