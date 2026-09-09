using Avalonia.Controls;
using Avalonia.Interactivity;
using StudioOneTools.App.Settings;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Avalonia.Services;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Avalonia;

public partial class UnArchiverWindow : Window
{
    #region Fields

    private readonly UnArchiverWindowViewModel _viewModel;
    private readonly ISongFolderUnarchiver      _unarchiver;
    private readonly IStorageDialogService      _storageDialogService = new StorageDialogService();

    #endregion

    #region Constructors

    public UnArchiverWindow()
    {
        InitializeComponent();

        _viewModel  = new UnArchiverWindowViewModel();
        _unarchiver = new SongFolderUnarchiver(new SongPathFixer());
        DataContext = _viewModel;

        var settings = UserSettingsService.Load();
        if (!string.IsNullOrWhiteSpace(settings.DefaultSongFolder) && Directory.Exists(settings.DefaultSongFolder))
        {
            _viewModel.DestinationFolder = settings.DefaultSongFolder;
        }
    }

    #endregion

    #region Event Handlers

    private async void BrowseArchiveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = await _storageDialogService.PickOpenFileAsync(
            this, "Choose the archive to extract", "Zip archive", ["zip"]);

        if (path is null)
        {
            return;
        }

        _viewModel.ArchiveFilePath = path;
    }

    private async void BrowseDestinationButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var initialPath = !string.IsNullOrWhiteSpace(_viewModel.DestinationFolder) &&
                          Directory.Exists(_viewModel.DestinationFolder)
            ? _viewModel.DestinationFolder
            : UserSettingsService.Load().DefaultSongFolder;

        var path = await _storageDialogService.PickFolderAsync(
            this, "Choose the folder where the song will be extracted.", initialPath);

        if (path is null)
        {
            return;
        }

        _viewModel.DestinationFolder = path;
    }

    private void ArchiveFilePathTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _viewModel.StatusMessage = "Choose an archive file and a destination folder.";
    }

    private void DestinationFolderTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        _viewModel.StatusMessage = "Choose an archive file and a destination folder.";
    }

    private async void ExtractButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var request = new SongUnarchiveRequest
        {
            ArchiveFilePath   = _viewModel.ArchiveFilePath,
            DestinationFolder = _viewModel.DestinationFolder,
        };

        _viewModel.IsBusy = true;

        try
        {
            var result = await Task.Run(() => _unarchiver.Unarchive(request));

            var pathMsg = result.PathsFixed > 0
                ? $" Fixed {result.PathsFixed} internal path{(result.PathsFixed == 1 ? "" : "s")}."
                : " Paths were already correct.";

            _viewModel.StatusMessage = $"Extracted to: {result.SongFolderPath}.{pathMsg}";

            var open = await MessageBoxService.ShowAsync(
                this,
                $"Extraction complete.{pathMsg}\n\nOpen the song folder?",
                "Done",
                AppMessageBoxButton.YesNo,
                AppMessageBoxIcon.Information);

            if (open == AppMessageBoxResult.Yes)
            {
                FileRevealService.Reveal(result.SongFolderPath);
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Extraction failed: {ex.Message}";
            await MessageBoxService.ShowAsync(this, ex.Message, "Extraction Failed", AppMessageBoxButton.OK, AppMessageBoxIcon.Error);
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    #endregion
}
