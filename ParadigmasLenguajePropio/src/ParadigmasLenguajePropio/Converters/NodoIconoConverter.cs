using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media.Imaging;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.Converters;

// "Caperta" es el nombre real del asset de carpeta cerrada
public class NodoIconoConverter : IValueConverter
{
    public static readonly NodoIconoConverter Instance = new();

    static readonly string[] ExtensionesImagen = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };

    static Bitmap Cargar(string archivo) =>
        new(Avalonia.Platform.AssetLoader.Open(new Uri($"avares://ParadigmasLenguajePropio/Assets/Icons/{archivo}")));

    static readonly Lazy<Bitmap> CarpetaAbierta = new(() => Cargar("Icon_CarpetaAbierta.png"));
    static readonly Lazy<Bitmap> CarpetaCerrada = new(() => Cargar("Icon_CapertaCerrada.png"));
    static readonly Lazy<Bitmap> DocumentoGgez = new(() => Cargar("Icon_logo.png"));
    static readonly Lazy<Bitmap> Imagenes = new(() => Cargar("Icon_Imagenes.png"));
    static readonly Lazy<Bitmap> DocumentoMd = new(() => Cargar("Icon_DocumentoMD.png"));
    static readonly Lazy<Bitmap> Documento = new(() => Cargar("Icon_NewDocumento.png"));

    public static Bitmap ObtenerIcono(NodoArchivos nodo)
    {
        if (nodo.EsCarpeta) return (nodo.Expandido ? CarpetaAbierta : CarpetaCerrada).Value;
        if (nodo.Nombre.EndsWith(".ggez", StringComparison.OrdinalIgnoreCase)) return DocumentoGgez.Value;
        if (ExtensionesImagen.Any(ext => nodo.Nombre.EndsWith(ext, StringComparison.OrdinalIgnoreCase))) return Imagenes.Value;
        if (nodo.Nombre.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return DocumentoMd.Value;
        return Documento.Value;
    }

    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is NodoArchivos nodo ? ObtenerIcono(nodo) : null;

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
