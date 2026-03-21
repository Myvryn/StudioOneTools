using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace StudioOneTools.App.Converters;

public sealed class ProgressToBrushConverter : IMultiValueConverter
{
    private static readonly System.Windows.Media.Color ProgressColor = System.Windows.Media.Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);
    private static readonly System.Windows.Media.Color SelectedColor = System.Windows.Media.Color.FromArgb(0xFF, 0xCF, 0xEA, 0xF8);
    private static readonly System.Windows.Media.Color DefaultColor  = Colors.White;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = values.Length > 0 && values[0] is bool selected && selected;
        var progress   = values.Length > 1 && values[1] is double p ? p : 0.0;
        var baseColor  = isSelected ? SelectedColor : DefaultColor;
        var normalizedProgress = Math.Min(Math.Max(progress, 0.0), 1.0);

        if (normalizedProgress <= 0)
        {
            return new SolidColorBrush(baseColor);
        }

        if (normalizedProgress >= 1.0)
        {
            return new SolidColorBrush(ProgressColor);
        }

        var gradientBrush = new LinearGradientBrush
        {
            StartPoint = new System.Windows.Point(0, 0),
            EndPoint = new System.Windows.Point(1, 0),
            GradientStops = new GradientStopCollection
            {
                new GradientStop(ProgressColor, 0),
                new GradientStop(ProgressColor, normalizedProgress),
                new GradientStop(baseColor, normalizedProgress),
                new GradientStop(baseColor, 1),
            },
        };

        return gradientBrush;
    }

    public object[] ConvertBack(object? value, Type[] targetTypes, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
