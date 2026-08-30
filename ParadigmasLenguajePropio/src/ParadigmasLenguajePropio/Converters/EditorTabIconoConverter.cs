using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

namespace ParadigmasLenguajePropio.Converters;

// mismo criterio que NodoIconoConverter/PanelCentral.IconoPara, para que pestaña y árbol
// muestren el mismo ícono por archivo.
public class EditorTabIconoConverter : IValueConverter
{
    public static readonly EditorTabIconoConverter Instance = new();

    static readonly string[] ExtensionesImagen = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    static Bitmap Cargar(string archivo) =>
        new(AssetLoader.Open(new Uri($"avares://ParadigmasLenguajePropio/Assets/Icons/{archivo}")));

    static readonly Lazy<Bitmap> DocumentoGgez = new(() => Cargar("Icon_logo.png"));
    static readonly Lazy<Bitmap> Imagenes = new(() => Cargar("Icon_Imagenes.png"));
    static readonly Lazy<Bitmap> DocumentoMd = new(() => Cargar("Icon_DocumentoMD.png"));
    static readonly Lazy<Bitmap> Documento = new(() => Cargar("Icon_NewDocumento.png"));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        string nombre = value as string ?? "";
        if (nombre.EndsWith(".ggez", StringComparison.OrdinalIgnoreCase)) return DocumentoGgez.Value;
        if (ExtensionesImagen.Any(ext => nombre.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) return Imagenes.Value;
        if (nombre.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return DocumentoMd.Value;
        return Documento.Value;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
