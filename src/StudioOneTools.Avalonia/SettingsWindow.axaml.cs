using Avalonia.Controls;
using Avalonia.Interactivity;
using StudioOneTools.App.Settings;
using StudioOneTools.Avalonia.Services;

namespace StudioOneTools.Avalonia;

public partial class SettingsWindow : Window
{
    #region Fields

    private readonly AppUserSettings _settings;
    private readonly IStorageDialogService _storageDialogService = new StorageDialogService();

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

    private async void BrowseSongFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = await _storageDialogService.PickFolderAsync(this, "Choose the default song folder.", _settings.DefaultSongFolder);

        if (path is null)
        {
            return;
        }

        _settings.DefaultSongFolder = path;
        DefaultSongFolderTextBox.Text = path;
    }

    private async void BrowseArchiveFolderButton_OnClick(object? sender, RoutedEventArgs e)
    {
        var path = await _storageDialogService.PickFolderAsync(this, "Choose the default archive folder.", _settings.DefaultArchiveFolder);

        if (path is null)
        {
            return;
        }

        _settings.DefaultArchiveFolder = path;
        DefaultArchiveFolderTextBox.Text = path;
    }

    private void OKButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(true);
    }

    private void CancelButton_OnClick(object? sender, RoutedEventArgs e)
    {
        Close(false);
    }

    #endregion

    #region Public Methods

    public AppUserSettings GetSettings() => _settings;

    #endregion
}
