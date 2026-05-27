using System.IO;
using System.Threading.Tasks;
using System.Windows;
using StudioOneTools.App.Settings;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Core.Contracts;
using StudioOneTools.Core.Models;
using StudioOneTools.StudioOne.Services;
using WinForms = System.Windows.Forms;

namespace StudioOneTools.App;

public partial class BackupWindow : Window
{
    #region Fields

    private readonly BackupWindowViewModel _viewModel;
    private readonly ISongFolderBackup     _backupService;
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

    private async void Window_OnLoaded(object sender, RoutedEventArgs e)
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

    private void BrowseSongFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description            = "Choose the root folder containing your Studio One song folders.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = false,
            SelectedPath           = _viewModel.RootFolderPath,
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        _viewModel.RootFolderPath = dialog.SelectedPath;
        PersistSettings();
    }

    private async void RootFolderPathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
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

    private void BrowseBackupFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description            = "Choose the folder where song backups will be stored.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = true,
            SelectedPath           = _viewModel.BackupFolderPath,
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        _viewModel.BackupFolderPath = dialog.SelectedPath;
        PersistSettings();
    }

    private void SelectAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectAll();
    }

    private void DeselectAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.DeselectAll();
    }

    private async void BackupButton_OnClick(object sender, RoutedEventArgs e)
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
            System.Windows.MessageBox.Show(this,
                "Please choose a backup destination folder.",
                "No Backup Folder",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
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
            System.Windows.MessageBox.Show(this, ex.Message, "Planning Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }
        finally
        {
            _viewModel.IsScanning = false;
        }

        if (plan.TotalFilesToCopy == 0)
        {
            _viewModel.StatusMessage = "All selected songs are already up to date in the backup location.";
            System.Windows.MessageBox.Show(this,
                "All selected songs are already up to date in the backup location.",
                "Nothing to Back Up",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
            return;
        }

        var confirmDialog = new BackupConfirmDialog(plan) { Owner = this };

        if (confirmDialog.ShowDialog() != true)
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

                System.Windows.MessageBox.Show(this,
                    $"{summary}\n\nSome files could not be copied:\n{errorList}{more}",
                    "Backup Complete with Errors",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            else
            {
                System.Windows.MessageBox.Show(this, summary, "Backup Complete", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }
        catch (Exception ex)
        {
            _viewModel.StatusMessage = $"Backup failed: {ex.Message}";
            System.Windows.MessageBox.Show(this, ex.Message, "Backup Failed", MessageBoxButton.OK, MessageBoxImage.Error);
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
