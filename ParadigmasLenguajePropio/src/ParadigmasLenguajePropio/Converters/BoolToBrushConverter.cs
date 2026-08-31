using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;

namespace ParadigmasLenguajePropio.Converters;

// generico para pinceles que cambian segun un bool (ej. rol de un mensaje de chat).
// ConverterParameter: "colorHexSiTrue|colorHexSiFalse"
public class BoolToBrushConverter : IValueConverter
{
    public static readonly BoolToBrushConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string param) return null;
        var partes = param.Split('|');
        bool esVerdadero = value is bool b && b;
        return new SolidColorBrush(Color.Parse(esVerdadero ? partes[0] : partes[1]));
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
