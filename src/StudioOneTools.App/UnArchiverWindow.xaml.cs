using System.Diagnostics;
using System.IO;
using System.Windows;
using StudioOneTools.App.Settings;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;
using Win32 = Microsoft.Win32;
using WinForms = System.Windows.Forms;

namespace StudioOneTools.App;

public partial class UnArchiverWindow : Window
{
    #region Fields

    private readonly UnArchiverWindowViewModel _viewModel;
    private readonly ISongFolderUnarchiver     _unarchiver;

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

    private void BrowseArchiveButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new Win32.OpenFileDialog
        {
            Title           = "Choose the archive to extract",
            Filter          = "Zip archive (*.zip)|*.zip",
            CheckFileExists = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _viewModel.ArchiveFilePath = dialog.FileName;
    }

    private void BrowseDestinationButton_OnClick(object sender, RoutedEventArgs e)
    {
        var initialPath = !string.IsNullOrWhiteSpace(_viewModel.DestinationFolder) &&
                          Directory.Exists(_viewModel.DestinationFolder)
            ? _viewModel.DestinationFolder
            : UserSettingsService.Load().DefaultSongFolder;

        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description            = "Choose the folder where the song will be extracted.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = true,
            InitialDirectory       = initialPath,
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        _viewModel.DestinationFolder = dialog.SelectedPath;
    }

    private void ArchiveFilePathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _viewModel.StatusMessage = "Choose an archive file and a destination folder.";
    }

    private void DestinationFolderTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        _viewModel.StatusMessage = "Choose an archive file and a destination folder.";
    }

    private async void ExtractButton_OnClick(object sender, RoutedEventArgs e)
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

            var open = System.Windows.MessageBox.Show(
                this,
                $"Extraction complete.{pathMsg}\n\nOpen the song folder?",
                "Done",
                MessageBoxButton.YesNo,
                MessageBoxImage.Information);

            if (open == MessageBoxResult.Yes)
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName        = "explorer.exe",
                    Arguments       = result.SongFolderPath,
                    UseShellExecute = true,
                });
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Extraction failed: {ex.Message}";
            System.Windows.MessageBox.Show(this, ex.Message, "Extraction Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsBusy = false;
        }
    }

    #endregion
}
