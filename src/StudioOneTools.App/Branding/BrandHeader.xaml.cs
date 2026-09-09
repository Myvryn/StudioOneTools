using System.Windows;
using UserControl = System.Windows.Controls.UserControl;

namespace StudioOneTools.App.Branding;

/// <summary>
///     The Six Walls wordmark strip shown at the top of every window:
///     mark + "SIX WALLS" + product line, with an optional per-screen title
///     (<see cref="Screen" />) and supporting caption (<see cref="Caption" />).
/// </summary>
public partial class BrandHeader : UserControl
{
    public static readonly DependencyProperty ProductProperty = DependencyProperty.Register(
        nameof(Product), typeof(string), typeof(BrandHeader),
        new PropertyMetadata("Tools for Studio One | Studio Pro"));

    public static readonly DependencyProperty ScreenProperty = DependencyProperty.Register(
        nameof(Screen), typeof(string), typeof(BrandHeader),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty CaptionProperty = DependencyProperty.Register(
        nameof(Caption), typeof(string), typeof(BrandHeader),
        new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty ScreenVisibilityProperty = DependencyProperty.Register(
        nameof(ScreenVisibility), typeof(Visibility), typeof(BrandHeader),
        new PropertyMetadata(Visibility.Collapsed));

    public static readonly DependencyProperty CaptionVisibilityProperty = DependencyProperty.Register(
        nameof(CaptionVisibility), typeof(Visibility), typeof(BrandHeader),
        new PropertyMetadata(Visibility.Collapsed));

    public BrandHeader() => InitializeComponent();

    /// <summary>Product line shown next to the wordmark.</summary>
    public string Product
    {
        get => (string)GetValue(ProductProperty);
        set => SetValue(ProductProperty, value);
    }

    /// <summary>Optional large per-screen title (e.g. "Song Archiver"). Hidden when empty.</summary>
    public string Screen
    {
        get => (string)GetValue(ScreenProperty);
        set => SetValue(ScreenProperty, value);
    }

    /// <summary>Optional supporting line under the title. Hidden when empty.</summary>
    public string Caption
    {
        get => (string)GetValue(CaptionProperty);
        set => SetValue(CaptionProperty, value);
    }

    public Visibility ScreenVisibility
    {
        get => (Visibility)GetValue(ScreenVisibilityProperty);
        private set => SetValue(ScreenVisibilityProperty, value);
    }

    public Visibility CaptionVisibility
    {
        get => (Visibility)GetValue(CaptionVisibilityProperty);
        private set => SetValue(CaptionVisibilityProperty, value);
    }

    private static void OnTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        var header = (BrandHeader)d;
        header.ScreenVisibility = string.IsNullOrWhiteSpace(header.Screen)
            ? Visibility.Collapsed
            : Visibility.Visible;
        header.CaptionVisibility = string.IsNullOrWhiteSpace(header.Caption)
            ? Visibility.Collapsed
            : Visibility.Visible;
    }
}
