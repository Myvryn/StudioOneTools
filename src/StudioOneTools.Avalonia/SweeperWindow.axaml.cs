using Avalonia.Controls;
using Avalonia.Interactivity;
using StudioOneTools.App.Settings;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Avalonia.Services;
using StudioOneTools.Core.Contracts;
using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Avalonia;

public partial class SweeperWindow : Window
{
    #region Fields

    private readonly SweeperWindowViewModel _viewModel;
    private readonly ISongFolderSweeper     _sweeper;
    private readonly IStorageDialogService  _storageDialogService = new StorageDialogService();
    private int                             _scanRequestId;

    #endregion

    #region Constructors

    public SweeperWindow()
    {
        InitializeComponent();

        _viewModel  = new SweeperWindowViewModel();
        _sweeper    = new SongFolderSweeper();
        DataContext = _viewModel;

        var settings = UserSettingsService.Load();
        if (!string.IsNullOrWhiteSpace(settings.DefaultSongFolder) && Directory.Exists(settings.DefaultSongFolder))
        {
            _viewModel.RootFolderPath = settings.DefaultSongFolder;
        }

        Loaded += OnLoaded;
    }

    private async void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_viewModel.RootFolderPath) && Directory.Exists(_viewModel.RootFolderPath))
        {
            await RunScanAsync(Path.GetFullPath(_viewModel.RootFolderPath), showErrorDialog: false);
        }
    }

    #endregion

    #region Event Handlers

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var initialPath = !string.IsNullOrWhiteSpace(_viewModel.RootFolderPath) && Directory.Exists(_viewModel.RootFolderPath)
            ? _viewModel.RootFolderPath
            : UserSettingsService.Load().DefaultSongFolder;

        var path = await _storageDialogService.PickFolderAsync(
            this, "Choose the root folder containing Studio One song folders.", initialPath);

        if (path is null)
        {
            return;
        }

        _viewModel.RootFolderPath = path;
    }

    private async void RootFolderPathTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.SetScanResults([]);

        if (string.IsNullOrWhiteSpace(_viewModel.RootFolderPath))
        {
            _viewModel.StatusMessage = "Choose a root folder to find candidates for deletion.";
            return;
        }

        var normalizedPath = Path.GetFullPath(_viewModel.RootFolderPath);

        if (!Directory.Exists(normalizedPath))
        {
            return;
        }

        var requestId = ++_scanRequestId;
        await Task.Delay(500);

        if (requestId != _scanRequestId)
        {
            return;
        }

        if (!string.Equals(normalizedPath, Path.GetFullPath(_viewModel.RootFolderPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await RunScanAsync(normalizedPath, showErrorDialog: false);
    }

    private async Task RunScanAsync(string rootFolderPath, bool showErrorDialog)
    {
        try
        {
            _viewModel.IsScanning    = true;
            _viewModel.StatusMessage = "Scanning…";

            var results = await Task.Run(() => _sweeper.Sweep(rootFolderPath));

            _viewModel.SetScanResults(results);
            _viewModel.StatusMessage = results.Count == 0
                ? "Scan complete. No folders flagged for deletion."
                : $"Scan complete. Found {results.Count} folder{(results.Count == 1 ? "" : "s")} flagged for review.";
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"Scan failed: {exception.Message}";
            if (showErrorDialog)
            {
                await MessageBoxService.ShowAsync(this, exception.Message, "Scan Failed", AppMessageBoxButton.OK, AppMessageBoxIcon.Error);
            }
        }
        finally
        {
            _viewModel.IsScanning = false;
        }
    }

    private void SelectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAll();
    }

    private void DeselectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.DeselectAll();
    }

    private async void DeleteButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var toDelete = _viewModel.FlaggedFolders.Where(f => f.IsSelected).ToList();

        if (toDelete.Count == 0)
        {
            return;
        }

        var folderWord = toDelete.Count == 1 ? "folder" : "folders";

        var confirm = await MessageBoxService.ShowAsync(
            this,
            $"Permanently delete {toDelete.Count} {folderWord}?\n\nThis cannot be undone.",
            "Confirm Deletion",
            AppMessageBoxButton.YesNo,
            AppMessageBoxIcon.Warning);

        if (confirm != AppMessageBoxResult.Yes)
        {
            return;
        }

        _viewModel.IsDeleting = true;

        var deleted = new List<SweepFolderItemViewModel>();
        var errors  = new List<string>();

        try
        {
            foreach (var item in toDelete)
            {
                try
                {
                    await Task.Run(() => Directory.Delete(item.FolderPath, recursive: true));
                    deleted.Add(item);
                }
                catch (Exception ex)
                {
                    errors.Add($"{item.FolderName}: {ex.Message}");
                }
            }
        }
        finally
        {
            _viewModel.IsDeleting = false;
        }

        foreach (var item in deleted)
        {
            _viewModel.RemoveItem(item);
        }

        if (_viewModel.RemoveFromRecentDocuments && deleted.Count > 0)
            StudioOneRecentDocumentsService.RemoveSongsInFolders(deleted.Select(d => d.FolderPath));

        var summary = $"Deleted {deleted.Count} of {toDelete.Count} {folderWord}.";
        _viewModel.StatusMessage = summary;

        var icon    = errors.Count == 0 ? AppMessageBoxIcon.Information : AppMessageBoxIcon.Warning;
        var message = errors.Count == 0
            ? summary
            : string.Join(Environment.NewLine, errors.Prepend(summary));

        await MessageBoxService.ShowAsync(this, message, "Deletion Complete", AppMessageBoxButton.OK, icon);
    }

    #endregion
}
