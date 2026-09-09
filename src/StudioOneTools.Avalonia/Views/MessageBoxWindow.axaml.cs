using Avalonia.Controls;
using StudioOneTools.Avalonia.Services;

namespace StudioOneTools.Avalonia.Views;

public partial class MessageBoxWindow : Window
{
    // Designer/XAML-loader constructor only — always use the parameterized constructor at call sites.
    public MessageBoxWindow()
    {
        InitializeComponent();
    }

    public MessageBoxWindow(string message, string title, AppMessageBoxButton buttons, AppMessageBoxIcon icon)
        : this()
    {
        Title = title;
        MessageText.Text = message;

        foreach (var (text, result, isDefault) in ButtonsFor(buttons))
        {
            var button = new Button
            {
                Content = text,
                MinWidth = 80,
                IsDefault = isDefault,
            };
            button.Click += (_, _) => Close(result);
            ButtonPanel.Children.Add(button);
        }
    }

    private static IEnumerable<(string Text, AppMessageBoxResult Result, bool IsDefault)> ButtonsFor(AppMessageBoxButton buttons) =>
        buttons switch
        {
            AppMessageBoxButton.OK =>
            [
                ("OK", AppMessageBoxResult.OK, true),
            ],
            AppMessageBoxButton.OKCancel =>
            [
                ("Cancel", AppMessageBoxResult.Cancel, false),
                ("OK", AppMessageBoxResult.OK, true),
            ],
            AppMessageBoxButton.YesNo =>
            [
                ("No", AppMessageBoxResult.No, false),
                ("Yes", AppMessageBoxResult.Yes, true),
            ],
            _ => throw new ArgumentOutOfRangeException(nameof(buttons)),
        };
}
