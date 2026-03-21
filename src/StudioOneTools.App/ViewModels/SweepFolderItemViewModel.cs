using StudioOneTools.Core.Models;

namespace StudioOneTools.App.ViewModels;

public sealed class SweepFolderItemViewModel : BindableBase
{
    #region Fields

    private bool _isSelected = true;

    #endregion

    #region Constructors

    public SweepFolderItemViewModel(SweepFolderResult result)
    {
        FolderPath = result.FolderPath;
        FolderName = result.FolderName;
        Reason     = result.Reason;
    }

    #endregion

    #region Properties

    public string FolderPath { get; }

    public string FolderName { get; }

    public string Reason     { get; }

    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }

    #endregion
}
