using System.Globalization;
using Avalonia.Data.Converters;

namespace ParadigmasLenguajePropio.Converters;

// Progreso 0..1 -> ancho del relleno, sobre el ancho fijo del contenedor en ExtensionsView.axaml
public class ProgresoAAnchoConverter : IValueConverter
{
    public static readonly ProgresoAAnchoConverter Instance = new();

    public const double AnchoMaximo = 150;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? Math.Clamp(d, 0, 1) * AnchoMaximo : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
