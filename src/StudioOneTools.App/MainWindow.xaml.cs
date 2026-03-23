using System.IO;
using System.Windows;
using System.Windows.Media;
using StudioOneTools.App.Settings;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;
using WinForms = System.Windows.Forms;
using Win32 = Microsoft.Win32;

namespace StudioOneTools.App;

public partial class MainWindow : Window
{
    #region Fields

    private readonly MainWindowViewModel  _viewModel;
    private readonly IStudioOneSongAnalyzer _songAnalyzer;
    private readonly ISongFolderArchiver    _songFolderArchiver;

    private SongAnalysisResult? _currentAnalysis;
    private MediaPlayer?         _mediaPlayer;
    private SongMediaFile?       _currentPlayingFile;
    private System.Windows.Threading.DispatcherTimer? _progressTimer;

    #endregion

    #region Constructors

    public MainWindow()
    {
        InitializeComponent();

        _viewModel          = new MainWindowViewModel();
        _songAnalyzer       = new StudioOneSongAnalyzer();
        _songFolderArchiver = new SongFolderArchiver(_songAnalyzer);

        DataContext = _viewModel;
    }

    public void LoadSongFolder(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(folderPath);

        if (!IsLoaded)
        {
            Loaded += (_, _) => LoadSongFolder(normalizedPath);
            return;
        }

        _viewModel.ArchiveFilePath = GetDefaultArchiveFilePath(normalizedPath);

        if (string.Equals(SourceFolderPathTextBox.Text, normalizedPath, StringComparison.OrdinalIgnoreCase))
        {
            SourceFolderPathTextBox_OnTextChanged(SourceFolderPathTextBox, new System.Windows.Controls.TextChangedEventArgs(System.Windows.Controls.TextBox.TextChangedEvent, System.Windows.Controls.UndoAction.None));
            return;
        }

        SourceFolderPathTextBox.Text = normalizedPath;
    }

    #endregion

    #region Event Handlers

    private void SettingsButton_OnClick(object sender, RoutedEventArgs e)
    {
        var settings = UserSettingsService.Load();
        var dialog   = new SettingsWindow(settings) { Owner = this };

        if (dialog.ShowDialog() == true)
        {
            UserSettingsService.Save(dialog.GetSettings());
        }
    }

    private void BrowseSongFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description         = "Choose the Studio One song folder to archive.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton = false,
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        _viewModel.SourceFolderPath = dialog.SelectedPath;
        _viewModel.ArchiveFilePath  = GetDefaultArchiveFilePath(dialog.SelectedPath);
        _viewModel.StatusMessage    = "Song folder selected. Run analysis to preview used and unused media.";

        ResetAnalysis();
    }

    private void BrowseArchiveFileButton_OnClick(object sender, RoutedEventArgs e)
    {
        var initialDirectory = string.IsNullOrWhiteSpace(_viewModel.SourceFolderPath)
            ? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments)
            : Path.GetDirectoryName(Path.GetFullPath(_viewModel.SourceFolderPath))
                ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        var dialog = new Win32.SaveFileDialog
        {
            AddExtension    = true,
            DefaultExt      = ".zip",
            Filter          = "Zip archive (*.zip)|*.zip",
            InitialDirectory = initialDirectory,
            FileName        = GetDefaultArchiveFileName(_viewModel.SourceFolderPath),
            OverwritePrompt = true,
            Title           = "Choose where to save the archive",
        };

        if (dialog.ShowDialog(this) != true)
        {
            return;
        }

        _viewModel.ArchiveFilePath = dialog.FileName;
        _viewModel.StatusMessage   = "Archive destination selected.";
    }

    private async Task HandleMultipleSongFiles(IReadOnlyList<SongFileInfo> songFiles)
    {
        var dialog = new SongSelectionDialog(songFiles) { Owner = this };
        dialog.ShowDialog();

        switch (dialog.Outcome)
        {
            case SongSelectionOutcome.AnalyzeSelected:
                if (dialog.SelectedSongFile is not null)
                {
                    RunAnalysis(dialog.SelectedSongFile.FilePath);
                }
                break;

            case SongSelectionOutcome.ArchiveAll:
                await RunArchiveAll(songFiles);
                break;

            // Cancel: do nothing
        }
    }

    private void RunAnalysis(string? songFilePath)
    {
        try
        {
            _currentAnalysis = _songAnalyzer.Analyze(_viewModel.SourceFolderPath, songFilePath);

            // Silently generate schema discovery (for future introspection needs)
            try
            {
                _ = _songAnalyzer.DiscoverSongStructure(songFilePath ?? _currentAnalysis.SongFilePath);
            }
            catch
            {
                // Schema discovery failures don't affect analysis
            }

            if (string.IsNullOrWhiteSpace(_viewModel.ArchiveFilePath))
            {
                _viewModel.ArchiveFilePath = GetDefaultArchiveFilePath(_viewModel.SourceFolderPath);
            }

            _viewModel.ApplyAnalysis(_currentAnalysis);
            _viewModel.StatusMessage = _currentAnalysis.HasMissingReferencedFiles
                ? "Analysis completed, but some referenced WAV files are missing. Fix the missing files before archiving."
                : "Analysis completed. Review the media list, then create the archive when ready.";
        }
        catch (Exception exception)
        {
            ResetAnalysis();
            _viewModel.StatusMessage = $"Analysis failed: {exception.Message}";
            System.Windows.MessageBox.Show(this, exception.Message, "Analysis Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private async Task RunArchiveAll(IReadOnlyList<SongFileInfo> songFiles)
    {
        var includeUnused   = AskAboutUnusedMediaFiles();
        var successes       = new List<string>();
        var errors          = new List<string>();
        var settings        = UserSettingsService.Load();
        var parentDirectory = Path.GetDirectoryName(Path.GetFullPath(_viewModel.SourceFolderPath))
                              ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        var archiveDirectory  = !string.IsNullOrWhiteSpace(settings.DefaultArchiveFolder) &&
                                Directory.Exists(settings.DefaultArchiveFolder)
            ? settings.DefaultArchiveFolder
            : parentDirectory;

        _viewModel.IsArchiving = true;

        try
        {
            foreach (var songFile in songFiles)
            {
                try
                {
                    var request = new SongArchiveRequest
                    {
                        SongFolderPath          = _viewModel.SourceFolderPath,
                        ArchiveFilePath         = Path.Combine(archiveDirectory, $"{songFile.FileName}.zip"),
                        SongFilePath            = songFile.FilePath,
                        RetainMixdownFiles      = _viewModel.RetainMixdownFiles,
                        RetainMasterFiles       = _viewModel.RetainMasterFiles,
                        IncludeUnusedMediaFiles = includeUnused,
                        DebugMode               = settings.DebugMode,
                    };

                    await Task.Run(() => _songFolderArchiver.CreateArchive(request));

                    successes.Add($"\u2713  {songFile.FileName}.zip");
                }
                catch (Exception ex)
                {
                    errors.Add($"\u2717  {songFile.FileName}: {ex.Message}");
                }
            }
        }
        finally
        {
            _viewModel.IsArchiving = false;
        }

        var summary = $"Archived {successes.Count} of {songFiles.Count} songs.";
        _viewModel.StatusMessage = summary;

        var lines = successes.Concat(errors).Prepend(summary);
        var icon  = errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning;
        System.Windows.MessageBox.Show(this, string.Join(Environment.NewLine, lines), "Archive All Complete", MessageBoxButton.OK, icon);
    }

    private async void ArchiveButton_OnClick(object sender, RoutedEventArgs e)
    {
        try
        {
            EnsureAnalysisIsCurrent();

            var includeUnused = _viewModel.UnusedWaveFileCount > 0 && AskAboutUnusedMediaFiles();
            var settings = UserSettingsService.Load();

            var request = new SongArchiveRequest
            {
                SongFolderPath          = _viewModel.SourceFolderPath,
                ArchiveFilePath         = _viewModel.ArchiveFilePath,
                SongFilePath            = _currentAnalysis?.SongFilePath,
                RetainMixdownFiles      = _viewModel.RetainMixdownFiles,
                RetainMasterFiles       = _viewModel.RetainMasterFiles,
                IncludeUnusedMediaFiles = includeUnused,
                DebugMode               = settings.DebugMode,
            };

            _viewModel.IsArchiving = true;

            var archiveResult = await Task.Run(() => _songFolderArchiver.CreateArchive(request));

            _viewModel.StatusMessage = $"Archive created successfully: {archiveResult.ArchiveFilePath}";

            // Show combined dialog with delete question and open folder checkbox
            var completeDialog = new ArchiveCompleteDialog(archiveResult.ArchiveFilePath) { Owner = this };
            if (completeDialog.ShowDialog() == true)
            {
                if (completeDialog.DeleteOriginalFolder)
                {
                    Directory.Delete(Path.GetFullPath(_viewModel.SourceFolderPath), recursive: true);
                    _viewModel.StatusMessage = $"Archive created and original folder deleted: {archiveResult.ArchiveFilePath}";
                    ResetAnalysis(clearPaths: true);
                }

                if (completeDialog.OpenArchiveFolder)
                {
                    var archiveDirectory = Path.GetDirectoryName(archiveResult.ArchiveFilePath);
                    if (!string.IsNullOrWhiteSpace(archiveDirectory) && Directory.Exists(archiveDirectory))
                    {
                        System.Diagnostics.Process.Start("explorer.exe", archiveDirectory);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"Archive failed: {exception.Message}";
            System.Windows.MessageBox.Show(this, exception.Message, "Archive Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            _viewModel.IsArchiving = false;
        }
    }

    private bool AskAboutUnusedMediaFiles()
    {
        return System.Windows.MessageBox.Show(
            this,
            "Include unused media files in the archive?\n\nSelecting No produces a smaller archive containing only files actively used by the song.",
            "Include Unused Media Files?",
            MessageBoxButton.YesNo,
            MessageBoxImage.Question) == MessageBoxResult.Yes;
    }

    private async void SourceFolderPathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        ResetAnalysis(preserveStatusMessage: true);

        // Auto-trigger analysis when a valid folder path is set
        if (string.IsNullOrWhiteSpace(_viewModel.SourceFolderPath))
        {
            return;
        }

        var normalizedPath = Path.GetFullPath(_viewModel.SourceFolderPath);

        if (!Directory.Exists(normalizedPath))
        {
            return;
        }

        // Give user a moment to stop typing the path before auto-analyzing
        await Task.Delay(500);

        // Only proceed if the path hasn't changed during the delay
        if (!string.Equals(normalizedPath, Path.GetFullPath(_viewModel.SourceFolderPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            var songFiles = _songAnalyzer.GetSongFiles(normalizedPath);

            if (songFiles.Count == 0)
            {
                _viewModel.StatusMessage = "No .song files found in the selected folder.";
                return;
            }

            if (songFiles.Count > 1)
            {
                await HandleMultipleSongFiles(songFiles);
                return;
            }

            RunAnalysis(songFiles[0].FilePath);
        }
        catch (Exception exception)
        {
            ResetAnalysis();
            _viewModel.StatusMessage = $"Analysis failed: {exception.Message}";
        }
    }

    private void PlayPreviewButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.IsPlaying)
        {
            StopPreview();
        }
        else
        {
            var filePath = _viewModel.PreviewFile?.FilePath;

            if (filePath is not null)
            {
                StartPreview(filePath);
            }
        }
    }

    private void PlayMediaFile_OnClick(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.Button button && button.DataContext is SongMediaFile mediaFile)
        {
            if (mediaFile.ExistsOnDisk && mediaFile.IsWaveFile)
            {
                try
                {
                    StartPreview(mediaFile.FullPath, mediaFile);
                    _viewModel.StatusMessage = $"Playing: {mediaFile.FileName}";
                }
                catch (Exception ex)
                {
                    System.Windows.MessageBox.Show(
                        this,
                        $"Failed to play audio file: {ex.Message}",
                        "Playback Error",
                        MessageBoxButton.OK,
                        MessageBoxImage.Error);
                }
            }
        }
    }

    #endregion

    #region Private Methods

    private void EnsureAnalysisIsCurrent()
    {
        if (_currentAnalysis is null)
        {
            throw new InvalidOperationException("Run analysis before creating the archive.");
        }

        var currentSourceFolder = Path.GetFullPath(_viewModel.SourceFolderPath);

        if (!string.Equals(_currentAnalysis.SongFolderPath, currentSourceFolder, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("The selected folder changed after analysis. Run analysis again before archiving.");
        }
    }

    private void ResetAnalysis(bool clearPaths = false, bool preserveStatusMessage = false)
    {
        StopPreview();
        _currentAnalysis = null;
        _viewModel.ClearAnalysis();

        if (clearPaths)
        {
            _viewModel.SourceFolderPath = string.Empty;
            _viewModel.ArchiveFilePath  = string.Empty;
        }

        if (!preserveStatusMessage)
        {
            _viewModel.StatusMessage = "Choose a Studio One song folder to begin.";
        }
    }

    private string GetDefaultArchiveFilePath(string sourceFolderPath)
    {
        var settings = UserSettingsService.Load();
        var defaultFolder = settings.DefaultArchiveFolder;

        var archiveDirectory = !string.IsNullOrWhiteSpace(defaultFolder) && Directory.Exists(defaultFolder)
            ? defaultFolder
            : Path.GetDirectoryName(Path.GetFullPath(sourceFolderPath))
              ?? Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        return Path.Combine(archiveDirectory, GetDefaultArchiveFileName(sourceFolderPath));
    }

    private static string GetDefaultArchiveFileName(string sourceFolderPath)
    {
        var folderName = string.IsNullOrWhiteSpace(sourceFolderPath)
            ? "StudioOneArchive"
            : new DirectoryInfo(Path.GetFullPath(sourceFolderPath)).Name;

        return $"{folderName}.zip";
    }

    private void StartPreview(string filePath, SongMediaFile? mediaFile = null)
    {
        StopPreview();

        _currentPlayingFile = mediaFile;

        _mediaPlayer              = new MediaPlayer();
        _mediaPlayer.MediaEnded  += (_, _) => Dispatcher.Invoke(StopPreview);
        _mediaPlayer.MediaFailed += (_, _) => Dispatcher.Invoke(StopPreview);
        _mediaPlayer.Open(new Uri(filePath, UriKind.Absolute));
        _mediaPlayer.Play();

        // Start progress timer if we're tracking a media file
        if (_currentPlayingFile != null)
        {
            _progressTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100),
            };
            _progressTimer.Tick += (_, _) => UpdateProgress();
            _progressTimer.Start();
        }

        _viewModel.IsPlaying = true;
    }

    private void UpdateProgress()
    {
        if (_mediaPlayer?.NaturalDuration.HasTimeSpan == true && _currentPlayingFile != null)
        {
            var duration = _mediaPlayer.NaturalDuration.TimeSpan.TotalSeconds;
            var current = _mediaPlayer.Position.TotalSeconds;

            if (duration > 0)
            {
                _currentPlayingFile.PlaybackProgress = current / duration;
            }
        }
    }

    private void StopPreview()
    {
        _progressTimer?.Stop();
        _progressTimer = null;

        if (_currentPlayingFile != null)
        {
            _currentPlayingFile.PlaybackProgress = 0.0;
        }
        _currentPlayingFile = null;

        _mediaPlayer?.Stop();
        _mediaPlayer?.Close();
        _mediaPlayer = null;

        _viewModel.IsPlaying = false;
    }

    protected override void OnClosed(EventArgs e)
    {
        StopPreview();
        base.OnClosed(e);
    }

    #endregion
}
