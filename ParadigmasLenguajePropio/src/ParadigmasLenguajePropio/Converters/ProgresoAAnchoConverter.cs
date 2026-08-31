using System.Globalization;
using Avalonia.Data.Converters;

namespace ParadigmasLenguajePropio.Converters;

// barra de "instalando" fantasma en ExtensionsView: progreso 0..1 -> ancho en pixeles del relleno,
// sobre el mismo ancho fijo que tiene el contenedor en el XAML (ver ExtensionsView.axaml)
public class ProgresoAAnchoConverter : IValueConverter
{
    public static readonly ProgresoAAnchoConverter Instance = new();

    public const double AnchoMaximo = 150;

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is double d ? Math.Clamp(d, 0, 1) * AnchoMaximo : 0.0;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
