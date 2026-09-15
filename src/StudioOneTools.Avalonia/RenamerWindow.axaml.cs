using Avalonia.Controls;
using Avalonia.Interactivity;
using StudioOneTools.App.Settings;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Avalonia.Services;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Avalonia;

public partial class RenamerWindow : Window
{
    #region Fields

    private readonly RenamerWindowViewModel _viewModel;
    private readonly ISongRenamer           _renamer;
    private readonly IStorageDialogService  _storageDialogService = new StorageDialogService();
    private readonly IAudioPreviewService   _audioPreview          = new AudioPreviewService();
    private          int                    _analyzeRequestId;

    #endregion

    #region Constructors

    public RenamerWindow()
    {
        InitializeComponent();

        _renamer    = new SongRenamer();
        _viewModel  = new RenamerWindowViewModel();
        DataContext = _viewModel;

        _audioPreview.Stopped += (_, _) => _viewModel.IsPlaying = false;

        var settings = UserSettingsService.Load();

        if (!string.IsNullOrWhiteSpace(settings.DefaultRenameFolder))
        {
            _viewModel.FolderPath = settings.DefaultRenameFolder;
        }

        Closed += (_, _) => StopPreview();
    }

    #endregion

    #region Event Handlers

    private async void Window_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.FolderPath))
        {
            return;
        }

        var path = Path.GetFullPath(_viewModel.FolderPath);

        if (Directory.Exists(path))
        {
            await RunAnalyzeAsync(path);
        }
    }

    private async void BrowseButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var settings = UserSettingsService.Load();

        var path = await _storageDialogService.PickFolderAsync(
            this,
            "Choose the Studio One song folder to rename.",
            !string.IsNullOrWhiteSpace(_viewModel.FolderPath) ? _viewModel.FolderPath : settings.DefaultSongFolder);

        if (path is null)
        {
            return;
        }

        _viewModel.FolderPath = path;
    }

    private async void FolderPathTextBox_OnTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (!IsLoaded)
        {
            return;
        }

        StopPreview();
        _viewModel.ClearAnalysis();

        if (string.IsNullOrWhiteSpace(_viewModel.FolderPath))
        {
            _viewModel.StatusMessage = "Browse to a Studio One song folder to get started.";
            return;
        }

        var normalizedPath = Path.GetFullPath(_viewModel.FolderPath);

        if (!Directory.Exists(normalizedPath))
        {
            return;
        }

        var requestId = ++_analyzeRequestId;
        await Task.Delay(500);

        if (requestId != _analyzeRequestId)
        {
            return;
        }

        if (!string.Equals(normalizedPath, Path.GetFullPath(_viewModel.FolderPath), StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        await RunAnalyzeAsync(normalizedPath);
    }

    private void SuggestionChip_OnClick(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { DataContext: string suggestion })
        {
            _viewModel.NewName = suggestion;
        }
    }

    private void PlayButton_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_viewModel.IsPlaying)
        {
            StopPreview();
        }
        else if (_viewModel.PreviewFilePath is not null)
        {
            StartPreview(_viewModel.PreviewFilePath);
        }
    }

    private async void RenameButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var oldName    = _viewModel.CurrentName;
        var newName    = _viewModel.NewName.Trim();
        var folderPath = _viewModel.FolderPath;

        if (string.IsNullOrWhiteSpace(newName))
        {
            return;
        }

        if (newName.IndexOfAny(Path.GetInvalidFileNameChars()) >= 0)
        {
            await MessageBoxService.ShowAsync(
                this,
                "The new name contains invalid characters.",
                "Invalid Name",
                AppMessageBoxButton.OK,
                AppMessageBoxIcon.Warning);
            return;
        }

        var confirm = await MessageBoxService.ShowAsync(
            this,
            $"Rename \"{oldName}\" to \"{newName}\"?\n\nThis will rename the folder, matching .song files, and Mixdown/Master audio files.",
            "Confirm Rename",
            AppMessageBoxButton.YesNo,
            AppMessageBoxIcon.Question);

        if (confirm != AppMessageBoxResult.Yes)
        {
            return;
        }

        StopPreview();

        try
        {
            _viewModel.IsRenaming    = true;
            _viewModel.StatusMessage = "Renaming…";

            var request = new SongRenameRequest
            {
                FolderPath = folderPath,
                OldName    = oldName,
                NewName    = newName,
            };

            var result = await _renamer.RenameAsync(request);

            PersistSettings(result.NewFolderPath);

            var fileWord = result.RenamedFileCount == 1 ? "file" : "files";
            var summary  = $"Successfully renamed to \"{newName}\". {result.RenamedFileCount} {fileWord} renamed.";

            _viewModel.StatusMessage = summary;
            _viewModel.FolderPath    = result.NewFolderPath;

            if (result.Errors.Count > 0)
            {
                var errorList = string.Join(Environment.NewLine, result.Errors.Take(10));
                var more      = result.Errors.Count > 10 ? $"\n\n…and {result.Errors.Count - 10} more errors." : string.Empty;

                await MessageBoxService.ShowAsync(
                    this,
                    $"{summary}\n\nSome items could not be renamed:\n{errorList}{more}",
                    "Rename Complete with Errors",
                    AppMessageBoxButton.OK,
                    AppMessageBoxIcon.Warning);
            }
            else
            {
                await MessageBoxService.ShowAsync(this, summary, "Rename Complete", AppMessageBoxButton.OK, AppMessageBoxIcon.Information);
            }

            // Re-analyse the newly named folder.
            if (Directory.Exists(result.NewFolderPath))
            {
                await RunAnalyzeAsync(result.NewFolderPath);
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Rename failed: {ex.Message}";
            await MessageBoxService.ShowAsync(this, ex.Message, "Rename Failed", AppMessageBoxButton.OK, AppMessageBoxIcon.Error);
        }
        finally
        {
            _viewModel.IsRenaming = false;
        }
    }

    #endregion

    #region Private Methods

    private async Task RunAnalyzeAsync(string folderPath)
    {
        try
        {
            _viewModel.IsAnalyzing   = true;
            _viewModel.StatusMessage = "Analysing…";

            var analysis = await Task.Run(() => _renamer.Analyze(folderPath));

            _viewModel.SetAnalysis(analysis);

            var parts = new List<string>
            {
                $"Ready to rename \"{analysis.CurrentName}\".",
            };

            if (analysis.NameSuggestions.Count > 0)
            {
                parts.Add($"Found {analysis.NameSuggestions.Count} name suggestion{(analysis.NameSuggestions.Count == 1 ? "" : "s")}.");
            }

            if (analysis.PreviewFile is not null)
            {
                parts.Add($"{analysis.PreviewFile.FileType} audio available.");
            }

            _viewModel.StatusMessage = string.Join(" ", parts);
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

    private void StartPreview(string filePath)
    {
        StopPreview();

        _audioPreview.Play(filePath);
        _viewModel.IsPlaying = true;
    }

    private void StopPreview()
    {
        _audioPreview.Stop();
        _viewModel.IsPlaying = false;
    }

    private void PersistSettings(string folderPath)
    {
        var settings = UserSettingsService.Load();
        settings.DefaultRenameFolder = folderPath;
        UserSettingsService.Save(settings);
    }

    #endregion
}
