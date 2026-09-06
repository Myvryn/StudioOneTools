using Avalonia.Controls;
using StudioOneTools.Avalonia.Views;

namespace StudioOneTools.Avalonia.Services;

public enum AppMessageBoxButton { OK, OKCancel, YesNo }

public enum AppMessageBoxIcon { None, Information, Warning, Error, Question }

public enum AppMessageBoxResult { None, OK, Cancel, Yes, No }

public static class MessageBoxService
{
    public static Task<AppMessageBoxResult> ShowAsync(
        Window owner,
        string message,
        string title,
        AppMessageBoxButton buttons = AppMessageBoxButton.OK,
        AppMessageBoxIcon icon = AppMessageBoxIcon.None)
    {
        var dialog = new MessageBoxWindow(message, title, buttons, icon);
        return dialog.ShowDialog<AppMessageBoxResult>(owner);
    }
}
