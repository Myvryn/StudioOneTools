using System.Collections.ObjectModel;
using StudioOneTools.Core.Models;

namespace StudioOneTools.App.ViewModels;

public sealed class MainWindowViewModel : BindableBase
{
    #region Fields

    private string           _sourceFolderPath   = string.Empty;
    private string           _archiveFilePath    = string.Empty;
    private string           _songName           = "Analysis Summary";
    private string           _songFilePath       = string.Empty;
    private string           _statusMessage      = "Choose a Studio One song folder to begin.";
    private string           _issuesSummary      = "No analysis has been run yet.";
    private bool             _retainMixdownFiles = true;
    private bool             _retainMasterFiles  = true;
    private int              _usedWaveFileCount;
    private int              _unusedWaveFileCount;
    private SongPreviewFile? _previewFile;
    private bool             _isPlaying;
    private bool             _isArchiving;

    #endregion

    #region Constructors

    public MainWindowViewModel()
    {
        MediaFiles = new ObservableCollection<SongMediaFile>();
    }

    #endregion

    #region Properties

    public ObservableCollection<SongMediaFile> MediaFiles { get; }

    public string SourceFolderPath
    {
        get => _sourceFolderPath;
        set => SetProperty(ref _sourceFolderPath, value);
    }

    public string ArchiveFilePath
    {
        get => _archiveFilePath;
        set
        {
            if (SetProperty(ref _archiveFilePath, value))
            {
                RaisePropertyChanged(nameof(CanArchive));
            }
        }
    }

    public string SongName
    {
        get => _songName;
        set => SetProperty(ref _songName, value);
    }

    public string SongFilePath
    {
        get => _songFilePath;
        set => SetProperty(ref _songFilePath, value);
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public string IssuesSummary
    {
        get => _issuesSummary;
        set => SetProperty(ref _issuesSummary, value);
    }

    public bool RetainMixdownFiles
    {
        get => _retainMixdownFiles;
        set => SetProperty(ref _retainMixdownFiles, value);
    }

    public bool RetainMasterFiles
    {
        get => _retainMasterFiles;
        set => SetProperty(ref _retainMasterFiles, value);
    }

    public int UsedWaveFileCount
    {
        get => _usedWaveFileCount;
        set => SetProperty(ref _usedWaveFileCount, value);
    }

    public int UnusedWaveFileCount
    {
        get => _unusedWaveFileCount;
        set => SetProperty(ref _unusedWaveFileCount, value);
    }

    public SongPreviewFile? PreviewFile
    {
        get => _previewFile;
        set
        {
            if (SetProperty(ref _previewFile, value))
            {
                RaisePropertyChanged(nameof(HasPreviewFile));
                RaisePropertyChanged(nameof(PreviewLabel));
            }
        }
    }

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

    public bool IsArchiving
    {
        get => _isArchiving;
        set
        {
            if (SetProperty(ref _isArchiving, value))
            {
                RaisePropertyChanged(nameof(CanArchive));
            }
        }
    }

    public bool HasPreviewFile => _previewFile is not null;

    public string PreviewLabel => _previewFile is null
        ? "Not available"
        : $"{_previewFile.FileType}: {_previewFile.FileName}";

    public string PlayButtonContent => _isPlaying ? "■  Stop" : "▶  Play";

    public bool CanArchive => !string.IsNullOrWhiteSpace(ArchiveFilePath) &&
                              UsedWaveFileCount >= 0 &&
                              !string.IsNullOrWhiteSpace(SongFilePath) &&
                              !HasMissingReferencedFiles &&
                              !IsArchiving;

    public bool HasMissingReferencedFiles => MediaFiles.Any(file => file.IsWaveFile && file.IsUsed && !file.ExistsOnDisk);

    #endregion

    #region Public Methods

    public void ClearAnalysis()
    {
        SongName            = "Analysis Summary";
        SongFilePath        = string.Empty;
        UsedWaveFileCount   = 0;
        UnusedWaveFileCount = 0;
        IssuesSummary       = "No analysis has been run yet.";
        PreviewFile         = null;
        IsPlaying           = false;

        MediaFiles.Clear();

        RaisePropertyChanged(nameof(HasMissingReferencedFiles));
        RaisePropertyChanged(nameof(CanArchive));
    }

    public void ApplyAnalysis(SongAnalysisResult analysis)
    {
        SongName            = analysis.SongName;
        SongFilePath        = analysis.SongFilePath;
        UsedWaveFileCount   = analysis.UsedWaveFiles.Count;
        UnusedWaveFileCount = analysis.UnusedWaveFiles.Count;
        PreviewFile         = analysis.PreviewFile;
        IssuesSummary       = analysis.Issues.Count == 0
            ? "No issues detected."
            : string.Join(Environment.NewLine, analysis.Issues);

        MediaFiles.Clear();

        foreach (var mediaFile in analysis.MediaFiles)
        {
            MediaFiles.Add(mediaFile);
        }

        RaisePropertyChanged(nameof(HasMissingReferencedFiles));
        RaisePropertyChanged(nameof(CanArchive));
    }

    #endregion
}

