using System.Collections.ObjectModel;
using System.ComponentModel;
using StudioOneTools.Core.Models;

namespace StudioOneTools.App.ViewModels;

public sealed class BackupWindowViewModel : BindableBase
{
    #region Fields

    private string _rootFolderPath   = string.Empty;
    private string _backupFolderPath = string.Empty;
    private string _statusMessage    = "Choose a root folder containing your Studio One songs.";
    private bool   _isScanning;
    private bool   _isBackingUp;

    #endregion

    #region Constructors

    public BackupWindowViewModel()
    {
        SongFolders = new ObservableCollection<BackupFolderItemViewModel>();
    }

    #endregion

    #region Properties

    public ObservableCollection<BackupFolderItemViewModel> SongFolders { get; }

    public string RootFolderPath
    {
        get => _rootFolderPath;
        set
        {
            if (SetProperty(ref _rootFolderPath, value))
            {
                RaisePropertyChanged(nameof(CanBackup));
            }
        }
    }

    public string BackupFolderPath
    {
        get => _backupFolderPath;
        set
        {
            if (SetProperty(ref _backupFolderPath, value))
            {
                RaisePropertyChanged(nameof(CanBackup));
            }
        }
    }

    public string StatusMessage
    {
        get => _statusMessage;
        set => SetProperty(ref _statusMessage, value);
    }

    public bool IsScanning
    {
        get => _isScanning;
        set
        {
            if (SetProperty(ref _isScanning, value))
            {
                RaisePropertyChanged(nameof(IsBusy));
                RaisePropertyChanged(nameof(BusyMessage));
                RaisePropertyChanged(nameof(CanBackup));
            }
        }
    }

    public bool IsBackingUp
    {
        get => _isBackingUp;
        set
        {
            if (SetProperty(ref _isBackingUp, value))
            {
                RaisePropertyChanged(nameof(IsBusy));
                RaisePropertyChanged(nameof(BusyMessage));
                RaisePropertyChanged(nameof(CanBackup));
            }
        }
    }

    public bool IsBusy => IsScanning || IsBackingUp;

    public string BusyMessage => IsScanning
        ? "Scanning folders, please wait…"
        : "Backing up songs, please wait…";

    public bool HasSongFolders => SongFolders.Count > 0;

    public int SelectedCount => SongFolders.Count(f => f.IsSelected);

    public bool CanBackup =>
        SelectedCount > 0 &&
        !string.IsNullOrWhiteSpace(BackupFolderPath) &&
        !IsBusy;

    public string BackupButtonText
    {
        get
        {
            var count = SelectedCount;
            return count == 1 ? "Backup 1 Song" : $"Backup {count} Songs";
        }
    }

    #endregion

    #region Public Methods

    public void SetScanResults(IReadOnlyList<SongBackupItem> items)
    {
        foreach (var item in SongFolders)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        SongFolders.Clear();

        foreach (var item in items)
        {
            var vm = new BackupFolderItemViewModel(item);
            vm.PropertyChanged += OnItemPropertyChanged;
            SongFolders.Add(vm);
        }

        RaiseCountProperties();
    }

    public void SelectAll()
    {
        foreach (var item in SongFolders)
        {
            item.IsSelected = true;
        }
    }

    public void DeselectAll()
    {
        foreach (var item in SongFolders)
        {
            item.IsSelected = false;
        }
    }

    #endregion

    #region Private Methods

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(BackupFolderItemViewModel.IsSelected), StringComparison.Ordinal))
        {
            RaiseCountProperties();
        }
    }

    private void RaiseCountProperties()
    {
        RaisePropertyChanged(nameof(HasSongFolders));
        RaisePropertyChanged(nameof(SelectedCount));
        RaisePropertyChanged(nameof(CanBackup));
        RaisePropertyChanged(nameof(BackupButtonText));
    }

    #endregion
}
