using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;

namespace ParadigmasLenguajePropio.Controls;

// tipografia compartida entre EditorGutter y EditorTextArea, y helper de medicion
// (reemplaza Win95Style.MeasureText para el editor: FormattedText mide en vez de la
// tabla de anchos por letra armada a mano).
static class EditorFont
{
    public static readonly Typeface Typeface = new(FontFamily.Parse(
        "avares://ParadigmasLenguajePropio/Assets/Fonts/LiberationSans-Regular.ttf#Liberation Sans"));

    // no hay un .ttf bold separado en Assets/Fonts - misma familia con FontWeight.Bold, Skia lo
    // emboldece de forma sintetica (usado por EditorMinimap, ver mas abajo)
    public static readonly Typeface TypefaceNegrita = new(FontFamily.Parse(
        "avares://ParadigmasLenguajePropio/Assets/Fonts/LiberationSans-Regular.ttf#Liberation Sans"),
        FontStyle.Normal, FontWeight.Bold);

    public static FormattedText Formatear(string texto, double fontSize, IBrush brush, bool negrita = false) =>
        new(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, negrita ? TypefaceNegrita : Typeface, fontSize, brush);

    // Medir()/MedirPrefijo() se llaman decenas de veces por linea (seleccion, guias de indentacion,
    // resaltado de brackets, TODO, click-to-column...) y CADA linea se vuelve a medir en CADA
    // keystroke porque EditorTextArea redibuja el documento entero (ver Render/MeasureOverride) -
    // sin cache, tipear una letra vuelve a mandar a shapear texto (FormattedText, el trabajo caro de
    // Skia) miles de veces incluso para lineas que no cambiaron. El contenido+tamaño de fuente es la
    // clave: el ancho de un mismo texto al mismo tamaño no cambia, asi que una vez medido alcanza.
    static readonly Dictionary<(string Texto, double FontSize), double> _cacheAnchos = new();
    const int LimiteCache = 50_000; // valvula de seguridad: una sesion larga no debe crecer sin tope

    public static double Medir(string texto, double fontSize)
    {
        if (texto.Length == 0) return 0;

        var clave = (texto, fontSize);
        if (_cacheAnchos.TryGetValue(clave, out double anchoCacheado)) return anchoCacheado;

        if (_cacheAnchos.Count >= LimiteCache) _cacheAnchos.Clear();

        double ancho = Formatear(texto, fontSize, Brushes.Black).Width;
        _cacheAnchos[clave] = ancho;
        return ancho;
    }

    // Medir() recorta el avance de los blancos AL FINAL del texto (por eso una corrida de solo
    // espacios mide 0, ver AnchoContinuacion en EditorTextArea) - para medir un prefijo de linea
    // que puede terminar en blanco (indentacion, espacios sobrantes) se le pega un caracter no-blanco
    // al final y se resta su ancho, asi el blanco queda "en el medio" y si cuenta.
    public static double MedirPrefijo(string texto, double fontSize) =>
        texto.Length == 0 ? 0 : Medir(texto + "|", fontSize) - Medir("|", fontSize);

    // alto de linea derivado del tamaño de fuente (antes una constante de 20px fija a los 18px de
    // fuente originales, ver zoom en EditorTextArea/EditorGutter): un unico lugar para esta cuenta
    // asi el gutter y el area de texto nunca pueden desalinearse entre si al hacer zoom.
    public static double AltoLinea(double fontSize) => fontSize + 2;
}
