using System.Windows;
using StudioOneTools.App.Settings;

namespace StudioOneTools.App;

public partial class HomeWindow : Window
{
    #region Fields

    private MainWindow?    _archiverWindow;
    private SweeperWindow? _sweeperWindow;

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

    #endregion
}
