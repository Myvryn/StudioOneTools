using System.Diagnostics;
using System.Windows;

namespace StudioOneTools.App;

public partial class ArchiveCompleteDialog : Window
{
    #region Properties

    public string ArchivePath { get; set; } = string.Empty;

    public bool DeleteOriginalFolder { get; private set; }

    public bool OpenArchiveFolder { get; private set; }

    #endregion

    #region Constructors

    public ArchiveCompleteDialog(string archivePath)
    {
        InitializeComponent();
        ArchivePath = archivePath;
    }

    #endregion

    #region Event Handlers

    private void YesButton_OnClick(object sender, RoutedEventArgs e)
    {
        DeleteOriginalFolder = true;
        OpenArchiveFolder = OpenFolderCheckBox.IsChecked ?? false;
        DialogResult = true;
        Close();
    }

    private void NoButton_OnClick(object sender, RoutedEventArgs e)
    {
        DeleteOriginalFolder = false;
        OpenArchiveFolder = OpenFolderCheckBox.IsChecked ?? false;
        DialogResult = true;
        Close();
    }

    #endregion
}
