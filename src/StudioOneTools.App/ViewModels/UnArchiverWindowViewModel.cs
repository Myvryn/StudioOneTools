using System.IO;

namespace StudioOneTools.App.ViewModels;

public sealed class UnArchiverWindowViewModel : BindableBase
{
    #region Fields

    private string _archiveFilePath    = string.Empty;
    private string _destinationFolder  = string.Empty;
    private string _statusMessage      = "Choose an archive file and a destination folder.";
    private bool   _isBusy;

    #endregion

    #region Properties

    public string ArchiveFilePath
    {
        get => _archiveFilePath;
        set
        {
            if (SetProperty(ref _archiveFilePath, value))
            {
                RaisePropertyChanged(nameof(ExtractedToPath));
                RaisePropertyChanged(nameof(CanExtract));
            }
        }
    }

    public string DestinationFolder
    {
        get => _destinationFolder;
        set
        {
            if (SetProperty(ref _destinationFolder, value))
            {
                RaisePropertyChanged(nameof(ExtractedToPath));
                RaisePropertyChanged(nameof(CanExtract));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaisePropertyChanged(nameof(CanExtract));
            }
        }
    }

    public string ExtractedToPath
    {
        get
        {
            if (string.IsNullOrWhiteSpace(ArchiveFilePath) || string.IsNullOrWhiteSpace(DestinationFolder))
                return string.Empty;

            var folderName = Path.GetFileNameWithoutExtension(ArchiveFilePath);
            return Path.Combine(DestinationFolder, folderName);
        }
    }

    public bool CanExtract =>
        !IsBusy &&
        !string.IsNullOrWhiteSpace(ArchiveFilePath) &&
        File.Exists(ArchiveFilePath) &&
        !string.IsNullOrWhiteSpace(DestinationFolder) &&
        Directory.Exists(DestinationFolder);

    #endregion
}
