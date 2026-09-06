using Avalonia.Controls;
using Avalonia.Interactivity;
using StudioOneTools.App.Settings;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Avalonia.Services;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;

namespace StudioOneTools.Avalonia;

public partial class BackupWindow : Window
{
    #region Fields

    private readonly BackupWindowViewModel _viewModel;
    private readonly ISongFolderBackup     _backupService;
    private readonly IStorageDialogService _storageDialogService = new StorageDialogService();
    private          int                   _scanRequestId;

    #endregion

    #region Constructors

    public BackupWindow()
    {
        InitializeComponent();

        _backupService = new SongFolderBackup(new StudioOneSongAnalyzer());
        _viewModel     = new BackupWindowViewModel();
        DataContext    = _viewModel;

        var settings = UserSettingsService.Load();

        if (!string.IsNullOrWhiteSpace(settings.DefaultSongFolder))
        {
            _viewModel.RootFolderPath = settings.DefaultSongFolder;
        }

        if (!string.IsNullOrWhiteSpace(settings.DefaultBackupFolder))
        {
            _viewModel.BackupFolderPath = settings.DefaultBackupFolder;
        }
    }

    #endregion

    #region Event Handlers

    private async void Window_OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(_viewModel.RootFolderPath))
        {
            return;
        }

        var path = Path.GetFullPath(_viewModel.RootFolderPath);

        if (Directory.Exists(path))
        {
            await RunScanAsync(path);
        }
    }

    private async void BrowseSongFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = await _storageDialogService.PickFolderAsync(
            this, "Choose the root folder containing your Studio One song folders.", _viewModel.RootFolderPath);

        if (path is null)
        {
            return;
        }

        _viewModel.RootFolderPath = path;
        PersistSettings();
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
            _viewModel.StatusMessage = "Choose a root folder containing your Studio One songs.";
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

        await RunScanAsync(normalizedPath);
    }

    private async void BrowseBackupFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = await _storageDialogService.PickFolderAsync(
            this, "Choose the folder where song backups will be stored.", _viewModel.BackupFolderPath);

        if (path is null)
        {
            return;
        }

        _viewModel.BackupFolderPath = path;
        PersistSettings();
    }

    private void SelectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.SelectAll();
    }

    private void DeselectAllButton_OnClick(object? sender, RoutedEventArgs e)
    {
        _viewModel.DeselectAll();
    }

    private async void BackupButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var selectedFolders = _viewModel.SongFolders
            .Where(f => f.IsSelected)
            .Select(f => f.FolderPath)
            .ToList();

        if (selectedFolders.Count == 0)
        {
            return;
        }

        var backupPath = _viewModel.BackupFolderPath;

        if (string.IsNullOrWhiteSpace(backupPath))
        {
            await MessageBoxService.ShowAsync(this,
                "Please choose a backup destination folder.",
                "No Backup Folder",
                AppMessageBoxButton.OK,
                AppMessageBoxIcon.Warning);
            return;
        }

        SongBackupPlan plan;

        try
        {
            _viewModel.IsScanning    = true;
            _viewModel.StatusMessage = "Planning backup…";
            plan = await _backupService.PlanBackupAsync(selectedFolders, backupPath);
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Failed to plan backup: {ex.Message}";
            await MessageBoxService.ShowAsync(this, ex.Message, "Planning Failed", AppMessageBoxButton.OK, AppMessageBoxIcon.Error);
            return;
        }
        finally
        {
            _viewModel.IsScanning = false;
        }

        if (plan.TotalFilesToCopy == 0)
        {
            _viewModel.StatusMessage = "All selected songs are already up to date in the backup location.";
            await MessageBoxService.ShowAsync(this,
                "All selected songs are already up to date in the backup location.",
                "Nothing to Back Up",
                AppMessageBoxButton.OK,
                AppMessageBoxIcon.Information);
            return;
        }

        var confirmDialog = new BackupConfirmDialog(plan);
        var confirmed     = await confirmDialog.ShowDialog<bool>(this);

        if (!confirmed)
        {
            _viewModel.StatusMessage = "Backup cancelled.";
            return;
        }

        var includeUnusedFiles = confirmDialog.IncludeUnusedFiles;

        try
        {
            _viewModel.IsBackingUp   = true;
            _viewModel.StatusMessage = "Backing up…";

            var progress = new Progress<string>(msg => _viewModel.StatusMessage = $"Copying: {msg}");
            var result   = await _backupService.ExecuteBackupAsync(plan, includeUnusedFiles, progress);

            PersistSettings();

            var songWord = result.FoldersProcessed == 1 ? "song" : "songs";
            var fileWord = result.FilesCopied == 1 ? "file" : "files";
            var summary  = $"Backup complete — {result.FilesCopied} {fileWord} copied across {result.FoldersProcessed} {songWord}.";

            _viewModel.StatusMessage = summary;

            if (result.Errors.Count > 0)
            {
                var errorList = string.Join(Environment.NewLine, result.Errors.Take(10));
                var more      = result.Errors.Count > 10 ? $"\n\n…and {result.Errors.Count - 10} more errors." : string.Empty;

                await MessageBoxService.ShowAsync(this,
                    $"{summary}\n\nSome files could not be copied:\n{errorList}{more}",
                    "Backup Complete with Errors",
                    AppMessageBoxButton.OK,
                    AppMessageBoxIcon.Warning);
            }
            else
            {
                await MessageBoxService.ShowAsync(this, summary, "Backup Complete", AppMessageBoxButton.OK, AppMessageBoxIcon.Information);
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Backup failed: {ex.Message}";
            await MessageBoxService.ShowAsync(this, ex.Message, "Backup Failed", AppMessageBoxButton.OK, AppMessageBoxIcon.Error);
        }
        finally
        {
            _viewModel.IsBackingUp = false;
        }
    }

    #endregion

    #region Private Methods

    private async Task RunScanAsync(string rootFolderPath)
    {
        try
        {
            _viewModel.IsScanning    = true;
            _viewModel.StatusMessage = "Scanning…";

            var results = await Task.Run(() => _backupService.GetSongFolders(rootFolderPath));

            _viewModel.SetScanResults(results);
            _viewModel.StatusMessage = results.Count == 0
                ? "No song folders found in the selected directory."
                : $"Found {results.Count} song folder{(results.Count == 1 ? "" : "s")}.";
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Scan failed: {ex.Message}";
        }
        finally
        {
            _viewModel.IsScanning = false;
        }
    }

    private void PersistSettings()
    {
        var settings = UserSettingsService.Load();

        if (!string.IsNullOrWhiteSpace(_viewModel.RootFolderPath))
        {
            settings.DefaultSongFolder = _viewModel.RootFolderPath;
        }

        if (!string.IsNullOrWhiteSpace(_viewModel.BackupFolderPath))
        {
            settings.DefaultBackupFolder = _viewModel.BackupFolderPath;
        }

        UserSettingsService.Save(settings);
    }

    #endregion
}
