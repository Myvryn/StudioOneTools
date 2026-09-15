using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Threading;
using StudioOneTools.App.Settings;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Avalonia.Services;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Avalonia;

public partial class SongArchiverWindow : Window
{
    #region Fields

    private readonly MainWindowViewModel    _viewModel;
    private readonly IStudioOneSongAnalyzer _songAnalyzer;
    private readonly ISongFolderArchiver    _songFolderArchiver;
    private readonly IStorageDialogService  _storageDialogService = new StorageDialogService();
    private readonly IAudioPreviewService   _audioPreview          = new AudioPreviewService();

    private SongAnalysisResult? _currentAnalysis;
    private SongMediaFile?      _currentPlayingFile;
    private DispatcherTimer?    _progressTimer;

    #endregion

    #region Constructors

    public SongArchiverWindow()
    {
        InitializeComponent();

        _viewModel          = new MainWindowViewModel();
        _songAnalyzer       = new StudioOneSongAnalyzer();
        _songFolderArchiver = new SongFolderArchiver(_songAnalyzer);

        DataContext = _viewModel;

        _audioPreview.Stopped += (_, _) => StopPreview();

        Closed += (_, _) => StopPreview();
    }

    #endregion

    #region Public Methods

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
            SourceFolderPathTextBox_OnTextChanged(SourceFolderPathTextBox, null!);
            return;
        }

        SourceFolderPathTextBox.Text = normalizedPath;
    }

    #endregion

    #region Event Handlers

    private async void SettingsButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = UserSettingsService.Load();
        var dialog   = new SettingsWindow(settings);

        if (await dialog.ShowDialog<bool>(this))
        {
            UserSettingsService.Save(dialog.GetSettings());
        }
    }

    private async void BrowseSongFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = await _storageDialogService.PickFolderAsync(
            this, "Choose the Studio One song folder to archive.", _viewModel.SourceFolderPath);

        if (path is null)
        {
            return;
        }

        _viewModel.SourceFolderPath = path;
        _viewModel.ArchiveFilePath  = GetDefaultArchiveFilePath(path);
        _viewModel.StatusMessage    = "Song folder selected. Run analysis to preview used and unused media.";

        ResetAnalysis();
    }

    private async void BrowseArchiveFileButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var initialDirectory = string.IsNullOrWhiteSpace(_viewModel.SourceFolderPath)
            ? null
            : Path.GetDirectoryName(Path.GetFullPath(_viewModel.SourceFolderPath));

        var path = await _storageDialogService.PickSaveFileAsync(
            this,
            "Choose where to save the archive",
            "Zip archive",
            ["zip"],
            GetDefaultArchiveFileName(_viewModel.SourceFolderPath),
            initialDirectory);

        if (path is null)
        {
            return;
        }

        _viewModel.ArchiveFilePath = path;
        _viewModel.StatusMessage   = "Archive destination selected.";
    }

    private async Task HandleMultipleSongFiles(IReadOnlyList<SongFileInfo> songFiles)
    {
        var dialog = new SongSelectionDialog(songFiles);
        await dialog.ShowDialog(this);

        switch (dialog.Outcome)
        {
            case SongSelectionOutcome.AnalyzeSelected:
                if (dialog.SelectedSongFile is not null)
                {
                    await RunAnalysisAsync(dialog.SelectedSongFile.FilePath);
                }
                break;

            case SongSelectionOutcome.ArchiveAll:
                await RunArchiveAll(songFiles);
                break;

            // Cancel: do nothing
        }
    }

    private async Task RunAnalysisAsync(string? songFilePath)
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
            await MessageBoxService.ShowAsync(this, exception.Message, "Analysis Failed", AppMessageBoxButton.OK, AppMessageBoxIcon.Error);
        }
    }

    private async Task RunArchiveAll(IReadOnlyList<SongFileInfo> songFiles)
    {
        var includeUnused   = await AskAboutUnusedMediaFiles();
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

                    successes.Add($"✓  {songFile.FileName}.zip");
                }
                catch (Exception ex)
                {
                    errors.Add($"✗  {songFile.FileName}: {ex.Message}");
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
        var icon  = errors.Count == 0 ? AppMessageBoxIcon.Information : AppMessageBoxIcon.Warning;
        await MessageBoxService.ShowAsync(this, string.Join(Environment.NewLine, lines), "Archive All Complete", AppMessageBoxButton.OK, icon);
    }

    private async void ArchiveButton_OnClick(object? sender, RoutedEventArgs e)
    {
        try
        {
            EnsureAnalysisIsCurrent();

            var includeUnused = _viewModel.UnusedWaveFileCount > 0 && await AskAboutUnusedMediaFiles();
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

            // Show combined dialog with post-archive options
            var songFolderPath = Path.GetFullPath(_viewModel.SourceFolderPath);
            var completeDialog = new ArchiveCompleteDialog(archiveResult.ArchiveFilePath);

            if (await completeDialog.ShowDialog<bool>(this))
            {
                if (completeDialog.RemoveFromRecentDocuments)
                    StudioOneRecentDocumentsService.RemoveSongsInFolders([songFolderPath]);

                if (completeDialog.DeleteOriginalFolder)
                {
                    Directory.Delete(songFolderPath, recursive: true);
                    _viewModel.StatusMessage = $"Archive created and original folder deleted: {archiveResult.ArchiveFilePath}";
                    ResetAnalysis(clearPaths: true);
                }

                if (completeDialog.OpenArchiveFolder)
                {
                    var archiveDirectory = Path.GetDirectoryName(archiveResult.ArchiveFilePath);
                    if (!string.IsNullOrWhiteSpace(archiveDirectory) && Directory.Exists(archiveDirectory))
                    {
                        FileRevealService.Reveal(archiveDirectory);
                    }
                }
            }
        }
        catch (Exception exception)
        {
            _viewModel.StatusMessage = $"Archive failed: {exception.Message}";
            await MessageBoxService.ShowAsync(this, exception.Message, "Archive Failed", AppMessageBoxButton.OK, AppMessageBoxIcon.Error);
        }
        finally
        {
            _viewModel.IsArchiving = false;
        }
    }

    private async Task<bool> AskAboutUnusedMediaFiles()
    {
        var result = await MessageBoxService.ShowAsync(
            this,
            "Include unused media files in the archive?\n\nSelecting No produces a smaller archive containing only files actively used by the song.",
            "Include Unused Media Files?",
            AppMessageBoxButton.YesNo,
            AppMessageBoxIcon.Question);

        return result == AppMessageBoxResult.Yes;
    }

    private async void SourceFolderPathTextBox_OnTextChanged(object? sender, TextChangedEventArgs? e)
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

            await RunAnalysisAsync(songFiles[0].FilePath);
        }
        catch (Exception exception)
        {
            ResetAnalysis();
            _viewModel.StatusMessage = $"Analysis failed: {exception.Message}";
        }
    }

    private void PlayPreviewButton_OnClick(object? sender, RoutedEventArgs e)
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

    private async void PlayMediaFile_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: SongMediaFile mediaFile })
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
                    await MessageBoxService.ShowAsync(
                        this,
                        $"Failed to play audio file: {ex.Message}",
                        "Playback Error",
                        AppMessageBoxButton.OK,
                        AppMessageBoxIcon.Error);
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
        _audioPreview.Play(filePath);

        if (_currentPlayingFile is not null)
        {
            _progressTimer = new DispatcherTimer
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
        if (_currentPlayingFile is null || _audioPreview.Duration <= TimeSpan.Zero)
        {
            return;
        }

        _currentPlayingFile.PlaybackProgress = _audioPreview.Elapsed.TotalSeconds / _audioPreview.Duration.TotalSeconds;
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

        _audioPreview.Stop();

        _viewModel.IsPlaying = false;
    }

    #endregion
}
