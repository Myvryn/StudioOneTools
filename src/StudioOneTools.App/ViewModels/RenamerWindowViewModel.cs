using System.Collections.ObjectModel;
using StudioOneTools.Core.Models;

namespace StudioOneTools.App.ViewModels;

public sealed class RenamerWindowViewModel : BindableBase
{
    #region Fields

    private string  _folderPath    = string.Empty;
    private string  _newName       = string.Empty;
    private string  _statusMessage = "Browse to a Studio One song folder to get started.";
    private string  _currentName   = string.Empty;
    private string? _previewFilePath;
    private string? _previewFileType;
    private bool    _isAnalyzing;
    private bool    _isRenaming;
    private bool    _hasAnalysis;
    private bool    _isDefaultName;
    private bool    _isPlaying;

    #endregion

    #region Constructors

    public RenamerWindowViewModel()
    {
        Suggestions.CollectionChanged += (_, _) => RaisePropertyChanged(nameof(HasSuggestions));
    }

    #endregion

    #region Properties

    public ObservableCollection<string> Suggestions { get; } = [];

    public string FolderPath
    {
        get => _folderPath;
        set
        {
            if (SetProperty(ref _folderPath, value))
            {
                RaisePropertyChanged(nameof(CanAnalyze));
            }
        }
    }

    public string NewName
    {
        get => _newName;
        set
        {
            if (SetProperty(ref _newName, value))
            {
                RaisePropertyChanged(nameof(CanRename));
            }
        }
    }

    public string CurrentName
    {
        get => _currentName;
        private set => SetProperty(ref _currentName, value);
    }

    public string? PreviewFilePath
    {
        get => _previewFilePath;
        private set
        {
            if (SetProperty(ref _previewFilePath, value))
            {
                RaisePropertyChanged(nameof(HasPreview));
                RaisePropertyChanged(nameof(PlayButtonContent));
            }
        }
    }

    public string? PreviewFileType
    {
        get => _previewFileType;
        private set
        {
            if (SetProperty(ref _previewFileType, value))
            {
                RaisePropertyChanged(nameof(PlayButtonContent));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsAnalyzing
    {
        get => _isAnalyzing;
        set
        {
            if (SetProperty(ref _isAnalyzing, value))
            {
                RaisePropertyChanged(nameof(IsBusy));
                RaisePropertyChanged(nameof(CanRename));
                RaisePropertyChanged(nameof(CanAnalyze));
            }
        }
    }

    public bool IsRenaming
    {
        get => _isRenaming;
        set
        {
            if (SetProperty(ref _isRenaming, value))
            {
                RaisePropertyChanged(nameof(IsBusy));
                RaisePropertyChanged(nameof(CanRename));
            }
        }
    }

    public bool IsBusy => _isAnalyzing || _isRenaming;

    public bool HasAnalysis
    {
        get => _hasAnalysis;
        private set
        {
            if (SetProperty(ref _hasAnalysis, value))
            {
                RaisePropertyChanged(nameof(HasNoAnalysis));
                RaisePropertyChanged(nameof(CanRename));
            }
        }
    }

    public bool HasNoAnalysis => !_hasAnalysis;

    public bool IsDefaultName
    {
        get => _isDefaultName;
        private set => SetProperty(ref _isDefaultName, value);
    }

    public bool HasPreview => _previewFilePath is not null;

    public bool HasSuggestions => Suggestions.Count > 0;

    public bool IsPlaying
    {
        get => _isPlaying;
        set
        {
            if (SetProperty(ref _isPlaying, value))
            {
                RaisePropertyChanged(nameof(PlayButtonContent));
            }
        }
    }

    public string PlayButtonContent => _isPlaying
        ? "Stop"
        : $"Play {_previewFileType ?? "Preview"}";

    public bool CanAnalyze => !string.IsNullOrWhiteSpace(_folderPath) && !IsBusy;

    public bool CanRename => _hasAnalysis &&
                             !string.IsNullOrWhiteSpace(_newName) &&
                             !string.Equals(_newName, _currentName, StringComparison.OrdinalIgnoreCase) &&
                             !IsBusy;

    #endregion

    #region Public Methods

    public void SetAnalysis(SongRenameAnalysis analysis)
    {
        CurrentName     = analysis.CurrentName;
        PreviewFilePath = analysis.PreviewFile?.FilePath;
        PreviewFileType = analysis.PreviewFile?.FileType;
        IsDefaultName   = analysis.IsDefaultName;

        Suggestions.Clear();
        foreach (var s in analysis.NameSuggestions)
        {
            Suggestions.Add(s);
        }

        HasAnalysis = true;
    }

    public void ClearAnalysis()
    {
        CurrentName     = string.Empty;
        PreviewFilePath = null;
        PreviewFileType = null;
        IsDefaultName   = false;
        NewName         = string.Empty;
        Suggestions.Clear();
        HasAnalysis     = false;
    }

    #endregion
}
