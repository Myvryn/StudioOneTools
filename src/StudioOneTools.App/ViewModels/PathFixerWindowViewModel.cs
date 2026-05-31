namespace StudioOneTools.App.ViewModels;

public sealed class PathFixerWindowViewModel : BindableBase
{
    #region Fields

    private string _songFilePath   = string.Empty;
    private string _storedPath     = string.Empty;
    private string _currentPath    = string.Empty;
    private string _statusMessage  = "Choose a .song file to check its internal paths.";
    private bool   _needsFixing;
    private int    _pathsToUpdate;
    private bool   _isAnalyzing;
    private bool   _isFixing;

    #endregion

    #region Properties

    public string SongFilePath
    {
        get => _songFilePath;
        set
        {
            if (SetProperty(ref _songFilePath, value))
            {
                RaisePropertyChanged(nameof(CanAnalyze));
            }
        }
    }

    public string StoredPath
    {
        get => _storedPath;
        set => SetProperty(ref _storedPath, value);
    }

    public string CurrentPath
    {
        get => _currentPath;
        set => SetProperty(ref _currentPath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool NeedsFixing
    {
        get => _needsFixing;
        set
        {
            if (SetProperty(ref _needsFixing, value))
            {
                RaisePropertyChanged(nameof(CanFix));
            }
        }
    }

    public int PathsToUpdate
    {
        get => _pathsToUpdate;
        set => SetProperty(ref _pathsToUpdate, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                RaisePropertyChanged(nameof(IsBusy));
                RaisePropertyChanged(nameof(BusyMessage));
                RaisePropertyChanged(nameof(CanAnalyze));
                RaisePropertyChanged(nameof(CanFix));
            }
        }
    }

    public bool IsFixing
    {
        get => _isFixing;
        set
        {
            if (SetProperty(ref _isFixing, value))
            {
                RaisePropertyChanged(nameof(IsBusy));
                RaisePropertyChanged(nameof(BusyMessage));
                RaisePropertyChanged(nameof(CanAnalyze));
                RaisePropertyChanged(nameof(CanFix));
            }
        }
    }

    public bool IsBusy    => IsAnalyzing || IsFixing;

    public string BusyMessage => IsFixing ? "Fixing paths, please wait…" : "Analyzing, please wait…";

    public bool CanAnalyze => !string.IsNullOrWhiteSpace(SongFilePath) && !IsBusy;

    public bool CanFix     => NeedsFixing && !IsBusy;

    #endregion

    #region Public Methods

    public void ClearAnalysis()
    {
        StoredPath    = string.Empty;
        CurrentPath   = string.Empty;
        NeedsFixing   = false;
        PathsToUpdate = 0;
    }

    #endregion
}
