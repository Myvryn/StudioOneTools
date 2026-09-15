using Avalonia.Controls;
using Avalonia.Interactivity;

namespace StudioOneTools.Avalonia;

public partial class ArchiveCompleteDialog : Window
{
    #region Properties

    public string ArchivePath { get; }

    public bool DeleteOriginalFolder { get; private set; }

    public bool OpenArchiveFolder { get; private set; }

    public bool RemoveFromRecentDocuments { get; private set; }

    #endregion

    #region Constructors

    public ArchiveCompleteDialog(string archivePath)
    {
        InitializeComponent();
        ArchivePath = archivePath;
    }

    #endregion

    #region Event Handlers

    private void YesButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DeleteOriginalFolder      = true;
        OpenArchiveFolder         = OpenFolderCheckBox.IsChecked ?? false;
        RemoveFromRecentDocuments = RemoveFromRecentDocsCheckBox.IsChecked ?? false;
        Close(true);
    }

    private void NoButton_OnClick(object? sender, RoutedEventArgs e)
    {
        DeleteOriginalFolder      = false;
        OpenArchiveFolder         = OpenFolderCheckBox.IsChecked ?? false;
        RemoveFromRecentDocuments = RemoveFromRecentDocsCheckBox.IsChecked ?? false;
        Close(true);
    }

    #endregion
}
