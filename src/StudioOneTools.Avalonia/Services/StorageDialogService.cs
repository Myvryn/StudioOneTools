using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace StudioOneTools.Avalonia.Services;

public sealed class StorageDialogService : IStorageDialogService
{
    public async Task<string?> PickFolderAsync(Window owner, string title, string? suggestedStartPath = null)
    {
        var startLocation = await ResolveStartFolderAsync(owner, suggestedStartPath);

        var result = await owner.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title                 = title,
            AllowMultiple         = false,
            SuggestedStartLocation = startLocation,
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    public async Task<string?> PickOpenFileAsync(Window owner, string title, string filterName, string[] extensions, string? suggestedStartPath = null)
    {
        var patterns      = extensions.Select(ext => $"*.{ext}").ToArray();
        var startLocation = await ResolveStartFolderAsync(owner, suggestedStartPath);

        var result = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title                  = title,
            AllowMultiple          = false,
            SuggestedStartLocation = startLocation,
            FileTypeFilter =
            [
                new FilePickerFileType(filterName) { Patterns = patterns },
            ],
        });

        return result.Count > 0 ? result[0].TryGetLocalPath() : null;
    }

    private static async Task<IStorageFolder?> ResolveStartFolderAsync(Window owner, string? suggestedStartPath)
    {
        if (string.IsNullOrWhiteSpace(suggestedStartPath) || !Directory.Exists(suggestedStartPath))
        {
            return null;
        }

        return await owner.StorageProvider.TryGetFolderFromPathAsync(suggestedStartPath);
    }
}
