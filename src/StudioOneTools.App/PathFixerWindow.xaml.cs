using System.IO;
using System.Windows;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Core.Contracts;
using StudioOneTools.StudioOne.Services;
using WinForms = System.Windows.Forms;
using Win32 = Microsoft.Win32;

namespace StudioOneTools.App;

public partial class PathFixerWindow : Window
{
    #region Fields

    private readonly PathFixerWindowViewModel _viewModel;
    private readonly ISongPathFixer           _pathFixer;
    private int                               _analyzeRequestId;

    #endregion

    #region Constructors

    public PathFixerWindow()
    {
        InitializeComponent();

        _viewModel  = new PathFixerWindowViewModel();
        _pathFixer  = new SongPathFixer();
        DataContext = _viewModel;
    }

    #endregion

    #region Event Handlers

    private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
        var initialDirectory = !string.IsNullOrWhiteSpace(_viewModel.SongFilePath) &&
                               File.Exists(_viewModel.SongFilePath)
            ? Path.GetDirectoryName(_viewModel.SongFilePath)
            : null;

        var dialog = new Win32.OpenFileDialog
        {
            Title            = "Choose the Studio One song file to check",
            Filter           = "Studio One Song (*.song)|*.song",
            InitialDirectory = initialDirectory ?? string.Empty,
            CheckFileExists  = true,
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _viewModel.SongFilePath = dialog.FileName;
    }

    private async void SongFilePathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        _viewModel.ClearAnalysis();

        if (string.IsNullOrWhiteSpace(_viewModel.SongFilePath))
        {
            _viewModel.StatusMessage = "Choose a .song file to check its internal paths.";
            return;
        }

        var requestId   = ++_analyzeRequestId;
        var songFilePath = _viewModel.SongFilePath;

        await Task.Delay(400);

        if (requestId != _analyzeRequestId)
        {
            return;
        }

        if (!File.Exists(songFilePath))
        {
            return;
        }

        await RunAnalysisAsync(songFilePath);
    }

    private async void FixButton_OnClick(object sender, RoutedEventArgs e)
    {
        var confirm = System.Windows.MessageBox.Show(
            this,
            "This will update the internal paths inside the .song file.\n\n" +
            "Make sure you have a backup before proceeding.\n\nContinue?",
            "Fix Paths",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        _viewModel.IsFixing = true;

        try
        {
            var songFilePath = _viewModel.SongFilePath;
            var result       = await Task.Run(() => _pathFixer.FixPaths(songFilePath));

            _viewModel.StatusMessage = $"Done. Updated {result.PathsUpdated} path{(result.PathsUpdated == 1 ? "" : "s")} in the song file.";

            // Re-analyze so the UI reflects the new state (paths should now match)
            await RunAnalysisAsync(songFilePath);
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Fix failed: {ex.Message}";
            System.Windows.MessageBox.Show(this, ex.Message, "Fix Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsFixing = false;
        }
    }

    #endregion

    #region Private Methods

    private async Task RunAnalysisAsync(string songFilePath)
    {
        _viewModel.IsAnalyzing = true;

        try
        {
            var analysis = await Task.Run(() => _pathFixer.Analyze(songFilePath));

            _viewModel.StoredPath    = analysis.StoredSongFolderPath;
            _viewModel.CurrentPath   = analysis.CurrentSongFolderPath;
            _viewModel.NeedsFixing   = analysis.NeedsFixing;
            _viewModel.PathsToUpdate = analysis.PathsToUpdate;

            _viewModel.StatusMessage = analysis.NeedsFixing
                ? $"Path mismatch detected. {analysis.PathsToUpdate} reference{(analysis.PathsToUpdate == 1 ? "" : "s")} need updating."
                : "Paths are correct — no fix needed.";
        }
        catch (Exception ex)
        {
            _viewModel.ClearAnalysis();
            _viewModel.StatusMessage = $"Analysis failed: {ex.Message}";
        }
        finally
        {
            _viewModel.IsAnalyzing = false;
        }
    }

    #endregion
}
