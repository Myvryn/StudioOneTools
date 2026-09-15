using System.Globalization;
using Avalonia;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace StudioOneTools.Avalonia.Converters;

public sealed class ProgressToBrushConverter : IMultiValueConverter
{
    private static readonly Color ProgressColor = Color.FromArgb(0xFF, 0x00, 0x78, 0xD4);
    private static readonly Color SelectedColor = Color.FromArgb(0xFF, 0xCF, 0xEA, 0xF8);
    private static readonly Color DefaultColor  = Colors.White;

    public object? Convert(IList<object?> values, Type targetType, object? parameter, CultureInfo culture)
    {
        var isSelected = values.Count > 0 && values[0] is bool selected && selected;
        var progress   = values.Count > 1 && values[1] is double p ? p : 0.0;
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

        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint   = new RelativePoint(1, 0, RelativeUnit.Relative),
            GradientStops =
            [
                new GradientStop(ProgressColor, 0),
                new GradientStop(ProgressColor, normalizedProgress),
                new GradientStop(baseColor, normalizedProgress),
                new GradientStop(baseColor, 1),
            ],
        };
    }
}
