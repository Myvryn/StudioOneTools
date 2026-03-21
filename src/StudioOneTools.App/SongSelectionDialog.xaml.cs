using System.Windows;
using StudioOneTools.Core.Models;

namespace StudioOneTools.App;

public enum SongSelectionOutcome
{
    Cancel,
    AnalyzeSelected,
    ArchiveAll,
}

public partial class SongSelectionDialog : Window
{
    #region Constructors

    public SongSelectionDialog(IReadOnlyList<SongFileInfo> songFiles)
    {
        InitializeComponent();

        SongFilesDataGrid.ItemsSource   = songFiles;
        SongFilesDataGrid.SelectedIndex = 0;
    }

    #endregion

    #region Properties

    public SongSelectionOutcome Outcome { get; private set; } = SongSelectionOutcome.Cancel;

    public SongFileInfo? SelectedSongFile => SongFilesDataGrid.SelectedItem as SongFileInfo;

    #endregion

    #region Event Handlers

    private void AnalyzeSelectedButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SongFilesDataGrid.SelectedItem is null)
        {
            return;
        }

        Outcome = SongSelectionOutcome.AnalyzeSelected;
        Close();
    }

    private void ArchiveAllButton_OnClick(object sender, RoutedEventArgs e)
    {
        Outcome = SongSelectionOutcome.ArchiveAll;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        Outcome = SongSelectionOutcome.Cancel;
        Close();
    }

    #endregion
}
