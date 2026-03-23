using System.IO;
using System.Windows;
using StudioOneTools.App.Settings;
using WinForms = System.Windows.Forms;

namespace StudioOneTools.App;

public partial class SettingsWindow : Window
{
    #region Fields

    private AppUserSettings _settings;

    #endregion

    #region Constructors

    public SettingsWindow(AppUserSettings settings)
    {
        InitializeComponent();

        _settings = new AppUserSettings
        {
            DefaultSongFolder    = settings.DefaultSongFolder,
            DefaultArchiveFolder = settings.DefaultArchiveFolder,
            DebugMode            = settings.DebugMode,
        };

        DataContext = _settings;
    }

    #endregion

    #region Event Handlers

    private void BrowseSongFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description            = "Choose the default song folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = true,
            SelectedPath           = _settings.DefaultSongFolder,
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        _settings.DefaultSongFolder = dialog.SelectedPath;
    }

    private void BrowseArchiveFolderButton_OnClick(object sender, RoutedEventArgs e)
    {
        using var dialog = new WinForms.FolderBrowserDialog
        {
            Description            = "Choose the default archive folder.",
            UseDescriptionForTitle = true,
            ShowNewFolderButton    = true,
            SelectedPath           = _settings.DefaultArchiveFolder,
        };

        if (dialog.ShowDialog() != WinForms.DialogResult.OK)
        {
            return;
        }

        _settings.DefaultArchiveFolder = dialog.SelectedPath;
    }

    private void OKButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void CancelButton_OnClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    #endregion

    #region Public Methods

    public AppUserSettings GetSettings() => _settings;

    #endregion
}
