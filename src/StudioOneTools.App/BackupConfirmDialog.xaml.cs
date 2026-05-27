using System.ComponentModel;
using System.Windows;
using StudioOneTools.Core.Models;

namespace StudioOneTools.App;

public partial class BackupConfirmDialog : Window
{
    #region Nested Types

    private sealed class FolderRow : INotifyPropertyChanged
    {
        private bool _includeUnused = true;

        public event PropertyChangedEventHandler? PropertyChanged;

        public required string FolderName    { get; init; }
        public required int    AllCount      { get; init; }
        public required int    ExcludedCount { get; init; }

        public bool IncludeUnused
        {
            get => _includeUnused;
            set
            {
                if (_includeUnused == value)
                {
                    return;
                }

                _includeUnused = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisplayCount)));
            }
        }

        public int DisplayCount => IncludeUnused ? AllCount : ExcludedCount;
    }

    #endregion

    #region Fields

    private readonly List<FolderRow> _rows;

    #endregion

    #region Constructors

    public BackupConfirmDialog(SongBackupPlan plan)
    {
        InitializeComponent();

        _rows = plan.Folders
            .Select(f => new FolderRow
            {
                FolderName    = f.FolderName,
                AllCount      = f.FileCount,
                ExcludedCount = f.FileCountExcludingUnused,
            })
            .ToList();

        FoldersGrid.ItemsSource = _rows;
        UpdateTotal();

        IncludeUnusedCheckBox.Checked   += IncludeUnused_OnToggled;
        IncludeUnusedCheckBox.Unchecked += IncludeUnused_OnToggled;
    }

    #endregion

    #region Properties

    public bool IncludeUnusedFiles => IncludeUnusedCheckBox.IsChecked == true;

    #endregion

    #region Event Handlers

    private void IncludeUnused_OnToggled(object sender, RoutedEventArgs e)
    {
        var include = IncludeUnusedCheckBox.IsChecked == true;

        foreach (var row in _rows)
        {
            row.IncludeUnused = include;
        }

        UpdateTotal();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void BackupButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    #endregion

    #region Private Methods

    private void UpdateTotal()
    {
        var include = IncludeUnusedCheckBox.IsChecked == true;
        var total   = _rows.Sum(r => include ? r.AllCount : r.ExcludedCount);
        TotalTextBlock.Text = $"Total: {total:N0} {(total == 1 ? "file" : "files")} to copy";
    }

    #endregion
}
