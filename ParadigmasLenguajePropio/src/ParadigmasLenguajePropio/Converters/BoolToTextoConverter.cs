using System.Globalization;
using Avalonia.Data.Converters;

namespace ParadigmasLenguajePropio.Converters;

// generico para botones cuyo texto cambia segun un bool (ej. Instalada -> "Desinstalar"/"Instalar").
// ConverterParameter: "textoSiTrue|textoSiFalse"
public class BoolToTextoConverter : IValueConverter
{
    public static readonly BoolToTextoConverter Instance = new();

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string param) return null;
        var partes = param.Split('|');
        bool esVerdadero = value is bool b && b;
        return esVerdadero ? partes[0] : partes[1];
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
