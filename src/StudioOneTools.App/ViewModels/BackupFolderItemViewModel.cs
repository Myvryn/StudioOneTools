using StudioOneTools.Core.Models;

namespace StudioOneTools.App.ViewModels;

public sealed class BackupFolderItemViewModel : BindableBase
{
    #region Fields

    private bool _isSelected = true;

    #endregion

    #region Constructors

    public BackupFolderItemViewModel(SongBackupItem item)
    {
        FolderPath = item.FolderPath;
        FolderName = item.FolderName;
    }

    #endregion

    #region Properties

    public string FolderPath { get; }

    public string FolderName { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    #endregion
}
