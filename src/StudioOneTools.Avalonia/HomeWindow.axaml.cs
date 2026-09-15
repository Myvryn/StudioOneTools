using Avalonia.Controls;
using Avalonia.Interactivity;
using StudioOneTools.Avalonia.Help;

namespace StudioOneTools.Avalonia;

public partial class HomeWindow : Window
{
    #region Fields

    private SweeperWindow?       _sweeperWindow;
    private BackupWindow?        _backupWindow;
    private PathFixerWindow?     _pathFixerWindow;
    private UnArchiverWindow?    _unArchiverWindow;
    private RenamerWindow?       _renamerWindow;
    private SongArchiverWindow?  _songArchiverWindow;

    #endregion

    #region Constructors

    public HomeWindow()
    {
        InitializeComponent();
    }

    #endregion

    #region Event Handlers

    private void SongArchiverCard_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_songArchiverWindow is null)
        {
            _songArchiverWindow        = new SongArchiverWindow();
            _songArchiverWindow.Closed += (_, _) => _songArchiverWindow = null;
            _songArchiverWindow.Show();
        }
        else
        {
            _songArchiverWindow.Activate();
        }
    }

    private void SweeperCard_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_sweeperWindow is null)
        {
            _sweeperWindow        = new SweeperWindow();
            _sweeperWindow.Closed += (_, _) => _sweeperWindow = null;
            _sweeperWindow.Show();
        }
        else
        {
            _sweeperWindow.Activate();
        }
    }

    private void BackupCard_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_backupWindow is null)
        {
            _backupWindow        = new BackupWindow();
            _backupWindow.Closed += (_, _) => _backupWindow = null;
            _backupWindow.Show();
        }
        else
        {
            _backupWindow.Activate();
        }
    }

    private void RenamerCard_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_renamerWindow is null)
        {
            _renamerWindow        = new RenamerWindow();
            _renamerWindow.Closed += (_, _) => _renamerWindow = null;
            _renamerWindow.Show();
        }
        else
        {
            _renamerWindow.Activate();
        }
    }

    private void UnArchiverCard_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_unArchiverWindow is null)
        {
            _unArchiverWindow        = new UnArchiverWindow();
            _unArchiverWindow.Closed += (_, _) => _unArchiverWindow = null;
            _unArchiverWindow.Show();
        }
        else
        {
            _unArchiverWindow.Activate();
        }
    }

    private void PathFixerCard_OnClick(object? sender, RoutedEventArgs e)
    {
        if (_pathFixerWindow is null)
        {
            _pathFixerWindow        = new PathFixerWindow();
            _pathFixerWindow.Closed += (_, _) => _pathFixerWindow = null;
            _pathFixerWindow.Show();
        }
        else
        {
            _pathFixerWindow.Activate();
        }
    }

    private void HelpButton_OnClick(object? sender, RoutedEventArgs e)
    {
        HelpService.Open();
    }

    #endregion
}
