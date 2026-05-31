using System.Windows;
using StudioOneTools.App.Help;
using StudioOneTools.App.Settings;

namespace StudioOneTools.App;

public partial class HomeWindow : Window
{
    #region Fields

    private MainWindow?         _archiverWindow;
    private SweeperWindow?      _sweeperWindow;
    private BackupWindow?       _backupWindow;
    private RenamerWindow?      _renamerWindow;
    private PathFixerWindow?    _pathFixerWindow;
    private UnArchiverWindow?   _unArchiverWindow;

    #endregion

    #region Constructors

    public HomeWindow()
    {
        InitializeComponent();
    }

    #endregion

    #region Event Handlers

    private void SongArchiverCard_OnClick(object sender, RoutedEventArgs e)
    {
        if (_archiverWindow is null)
        {
            _archiverWindow        = new MainWindow();
            _archiverWindow.Closed += (_, _) => _archiverWindow = null;
            _archiverWindow.Show();
        }
        else
        {
            _archiverWindow.Activate();
        }
    }

    private void SweeperCard_OnClick(object sender, RoutedEventArgs e)
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

    private void BackupCard_OnClick(object sender, RoutedEventArgs e)
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

    private void RenamerCard_OnClick(object sender, RoutedEventArgs e)
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

    private void UnArchiverCard_OnClick(object sender, RoutedEventArgs e)
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

    private void PathFixerCard_OnClick(object sender, RoutedEventArgs e)
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

    private void HelpButton_OnClick(object sender, RoutedEventArgs e)
    {
        HelpService.Open();
    }

    #endregion
}
