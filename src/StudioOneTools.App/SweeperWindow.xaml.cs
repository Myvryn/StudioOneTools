using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Threading.Tasks;
using StudioOneTools.App.ViewModels;
using StudioOneTools.Core.Contracts;
using StudioOneTools.StudioOne.Services;
using WinForms = System.Windows.Forms;

namespace StudioOneTools.App;

public partial class SweeperWindow : Window
{
    #region Fields

    private readonly SweeperWindowViewModel _viewModel;
    private readonly ISongFolderSweeper     _sweeper;
    private MainWindow?                     _archiverWindow;
    private int                             _scanRequestId;

    #endregion

    #region Constructors

    public SweeperWindow()
    {
        InitializeComponent();

        _viewModel  = new SweeperWindowViewModel();
        _sweeper    = new SongFolderSweeper();
        DataContext = _viewModel;
    }

    #endregion

    #region Event Handlers

    private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description            = "Choose the root folder containing Studio One song folders.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = false,
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        _viewModel.RootFolderPath = dialog.SelectedPath;
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
                System.Windows.MessageBox.Show(this, exception.Message, "Scan Failed", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
        finally
        {
            _viewModel.IsScanning = false;
        }
    }

    private void SelectAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.SelectAll();
    }

    private void DeselectAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        _viewModel.DeselectAll();
    }

    private async void DeleteButton_OnClick(object sender, RoutedEventArgs e)
    {
        var toDelete = _viewModel.FlaggedFolders.Where(f => f.IsSelected).ToList();

        if (toDelete.Count == 0)
        {
            return;
        }

        var folderWord = toDelete.Count == 1 ? "folder" : "folders";

        var confirm = System.Windows.MessageBox.Show(
            this,
            $"Permanently delete {toDelete.Count} {folderWord}?\n\nThis cannot be undone.",
            "Confirm Deletion",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
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

        var summary    = $"Deleted {deleted.Count} of {toDelete.Count} {folderWord}.";
        _viewModel.StatusMessage = summary;

        var icon    = errors.Count == 0 ? MessageBoxImage.Information : MessageBoxImage.Warning;
        var message = errors.Count == 0
            ? summary
            : string.Join(Environment.NewLine, errors.Prepend(summary));

        System.Windows.MessageBox.Show(this, message, "Deletion Complete", MessageBoxButton.OK, icon);
    }

    private void OpenInExplorerMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedSweepItem() is not SweepFolderItemViewModel item)
        {
            return;
        }

        OpenFolderInExplorer(item.FolderPath);
    }

    private void SendToArchiverMenuItem_OnClick(object sender, RoutedEventArgs e)
    {
        if (GetSelectedSweepItem() is not SweepFolderItemViewModel item)
        {
            return;
        }

        if (_archiverWindow is null)
        {
            _archiverWindow = new MainWindow();
            _archiverWindow.Closed += (_, _) => _archiverWindow = null;
            _archiverWindow.Show();
        }
        else
        {
            _archiverWindow.Activate();
        }

        _archiverWindow.LoadSongFolder(item.FolderPath);
    }

    private void ResultsDataGrid_OnPreviewMouseRightButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        var dependencyObject = e.OriginalSource as DependencyObject;

        while (dependencyObject is not null && dependencyObject is not System.Windows.Controls.DataGridRow)
        {
            dependencyObject = VisualTreeHelper.GetParent(dependencyObject);
        }

        if (dependencyObject is System.Windows.Controls.DataGridRow row)
        {
            row.IsSelected = true;
            row.Focus();
        }
    }

    private SweepFolderItemViewModel? GetSelectedSweepItem()
    {
        return ResultsDataGrid.SelectedItem as SweepFolderItemViewModel;
    }

    private void OpenFolderInExplorer(string folderPath)
    {
        if (string.IsNullOrWhiteSpace(folderPath))
        {
            return;
        }

        if (!Directory.Exists(folderPath))
        {
            System.Windows.MessageBox.Show(this, $"The folder was not found:{Environment.NewLine}{folderPath}", "Folder Not Found", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = folderPath,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"Failed to open folder:{Environment.NewLine}{ex.Message}", "Open Folder Failed", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    #endregion
}
