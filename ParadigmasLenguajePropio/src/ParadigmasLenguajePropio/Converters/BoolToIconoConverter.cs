using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ParadigmasLenguajePropio.Converters;

// ConverterParameter: "nombreSiTrue|nombreSiFalse"
public class BoolToIconoConverter : IValueConverter
{
    public static readonly BoolToIconoConverter Instance = new();

    static readonly Dictionary<string, Bitmap> Cache = new();

    static Bitmap Cargar(string archivo)
    {
        if (Cache.TryGetValue(archivo, out var existente)) return existente;
        var bitmap = new Bitmap(AssetLoader.Open(new Uri($"avares://ParadigmasLenguajePropio/Assets/Icons/{archivo}")));
        Cache[archivo] = bitmap;
        return bitmap;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        if (parameter is not string param) return null;
        var partes = param.Split('|');
        bool esVerdadero = value is bool b && b;
        return Cargar(esVerdadero ? partes[0] : partes[1]);
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
