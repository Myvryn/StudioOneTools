using System.Collections.ObjectModel;
using System.ComponentModel;
using StudioOneTools.Core.Models;

namespace StudioOneTools.App.ViewModels;

public sealed class SweeperWindowViewModel : BindableBase
{
    #region Fields

    private string _rootFolderPath = string.Empty;
    private string _statusMessage  = "Choose a root folder to find candidates for deletion.";
    private bool   _isScanning;
    private bool   _isDeleting;

    #endregion

    #region Constructors

    public SweeperWindowViewModel()
    {
        FlaggedFolders = new ObservableCollection<SweepFolderItemViewModel>();
    }

    #endregion

    #region Properties

    public ObservableCollection<SweepFolderItemViewModel> FlaggedFolders { get; }

    public string RootFolderPath
    {
        get => _rootFolderPath;
        set
        {
            if (SetProperty(ref _rootFolderPath, value))
            {
                RaisePropertyChanged(nameof(CanScan));
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
                RaisePropertyChanged(nameof(CanScan));
                RaisePropertyChanged(nameof(CanDelete));
            }
        }
    }

    public bool IsDeleting
    {
        get => _isDeleting;
        set
        {
            if (SetProperty(ref _isDeleting, value))
            {
                RaisePropertyChanged(nameof(IsBusy));
                RaisePropertyChanged(nameof(BusyMessage));
                RaisePropertyChanged(nameof(CanScan));
                RaisePropertyChanged(nameof(CanDelete));
            }
        }
    }

    public bool IsBusy => IsScanning || IsDeleting;

    public string BusyMessage => IsScanning
        ? "Scanning folders, please wait\u2026"
        : "Deleting folders, please wait\u2026";

    public bool HasFlaggedFolders => FlaggedFolders.Count > 0;

    public int SelectedCount => FlaggedFolders.Count(f => f.IsSelected);

    public string DeleteButtonText
    {
        get
        {
            var count = SelectedCount;
            return count == 1 ? "Delete 1 Selected Folder" : $"Delete {count} Selected Folders";
        }
    }

    public bool CanScan   => !string.IsNullOrWhiteSpace(RootFolderPath) && !IsScanning && !IsDeleting;

    public bool CanDelete => SelectedCount > 0 && !IsScanning && !IsDeleting;

    #endregion

    #region Public Methods

    public void SetScanResults(IReadOnlyList<SweepFolderResult> results)
    {
        foreach (var item in FlaggedFolders)
        {
            item.PropertyChanged -= OnItemPropertyChanged;
        }

        FlaggedFolders.Clear();

        foreach (var result in results)
        {
            var item = new SweepFolderItemViewModel(result);
            item.PropertyChanged += OnItemPropertyChanged;
            FlaggedFolders.Add(item);
        }

        RaiseCountProperties();
    }

    public void RemoveItem(SweepFolderItemViewModel item)
    {
        item.PropertyChanged -= OnItemPropertyChanged;
        FlaggedFolders.Remove(item);
        RaiseCountProperties();
    }

    public void SelectAll()
    {
        foreach (var item in FlaggedFolders)
        {
            item.IsSelected = true;
        }
    }

    public void DeselectAll()
    {
        foreach (var item in FlaggedFolders)
        {
            item.IsSelected = false;
        }
    }

    #endregion

    #region Private Methods

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (string.Equals(e.PropertyName, nameof(SweepFolderItemViewModel.IsSelected), StringComparison.Ordinal))
        {
            RaiseCountProperties();
        }
    }

    private void RaiseCountProperties()
    {
        RaisePropertyChanged(nameof(HasFlaggedFolders));
        RaisePropertyChanged(nameof(SelectedCount));
        RaisePropertyChanged(nameof(CanDelete));
        RaisePropertyChanged(nameof(DeleteButtonText));
    }

    #endregion
}
