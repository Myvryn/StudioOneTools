using Avalonia.Controls;

namespace StudioOneTools.Avalonia.Services;

public interface IStorageDialogService
{
    Task<string?> PickFolderAsync(Window owner, string title, string? suggestedStartPath = null);

    Task<string?> PickOpenFileAsync(Window owner, string title, string filterName, string[] extensions, string? suggestedStartPath = null);
}
