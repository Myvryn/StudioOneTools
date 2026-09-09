using Avalonia;
using Avalonia.Controls;

namespace StudioOneTools.Avalonia.Branding;

/// <summary>
///     The Six Walls wordmark strip shown at the top of every window:
///     mark + "SIX WALLS" + product line, with an optional per-screen title
///     (<see cref="Screen" />) and supporting caption (<see cref="Caption" />).
/// </summary>
public partial class BrandHeader : UserControl
{
    public static readonly StyledProperty<string> ProductProperty =
        AvaloniaProperty.Register<BrandHeader, string>(
            nameof(Product), "Tools for Studio One | Studio Pro");

    public static readonly StyledProperty<string> ScreenProperty =
        AvaloniaProperty.Register<BrandHeader, string>(nameof(Screen), string.Empty);

    public static readonly StyledProperty<string> CaptionProperty =
        AvaloniaProperty.Register<BrandHeader, string>(nameof(Caption), string.Empty);

    public static readonly DirectProperty<BrandHeader, bool> HasScreenProperty =
        AvaloniaProperty.RegisterDirect<BrandHeader, bool>(nameof(HasScreen), o => o.HasScreen);

    public static readonly DirectProperty<BrandHeader, bool> HasCaptionProperty =
        AvaloniaProperty.RegisterDirect<BrandHeader, bool>(nameof(HasCaption), o => o.HasCaption);

    public BrandHeader() => InitializeComponent();

    /// <summary>Product line shown next to the wordmark.</summary>
    public string Product
    {
        get => GetValue(ProductProperty);
        set => SetValue(ProductProperty, value);
    }

    /// <summary>Optional large per-screen title (e.g. "Song Backup"). Hidden when empty.</summary>
    public string Screen
    {
        get => GetValue(ScreenProperty);
        set => SetValue(ScreenProperty, value);
    }

    /// <summary>Optional supporting line under the title. Hidden when empty.</summary>
    public string Caption
    {
        get => GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public bool HasScreen => !string.IsNullOrWhiteSpace(Screen);
    public bool HasCaption => !string.IsNullOrWhiteSpace(Caption);

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == ScreenProperty)
            RaisePropertyChanged(HasScreenProperty, !HasScreen, HasScreen);
        else if (change.Property == CaptionProperty)
            RaisePropertyChanged(HasCaptionProperty, !HasCaption, HasCaption);
    }
}
