using System.Globalization;
using Avalonia.Controls;
using Avalonia.Data.Converters;

namespace ParadigmasLenguajePropio.Converters;

// permite bindear ColumnDefinition.Width/RowDefinition.Height (GridLength) directo a un double del ViewModel,
// para que GridSplitter pueda escribirle de vuelta al arrastrar.
public class PixelGridLengthConverter : IValueConverter
{
    public static readonly PixelGridLengthConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? new GridLength(d, GridUnitType.Pixel) : new GridLength(0, GridUnitType.Pixel);

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is GridLength g ? g.Value : 0d;
}
