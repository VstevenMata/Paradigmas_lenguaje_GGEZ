using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;

namespace ParadigmasLenguajePropio.Controls;

// tipografia compartida entre EditorGutter, EditorTextArea y EditorMinimap, con cache de shaping
static class EditorFont
{
    public static readonly Typeface Typeface = new(FontFamily.Parse(
        "avares://ParadigmasLenguajePropio/Assets/Fonts/LiberationSans-Regular.ttf#Liberation Sans"));

    // no hay un .ttf bold separado en Assets/Fonts - misma familia con FontWeight.Bold, Skia lo
    // emboldece de forma sintetica (usado por EditorMinimap, ver mas abajo)
    public static readonly Typeface TypefaceNegrita = new(FontFamily.Parse(
        "avares://ParadigmasLenguajePropio/Assets/Fonts/LiberationSans-Regular.ttf#Liberation Sans"),
        FontStyle.Normal, FontWeight.Bold);

    // Render() corre en cada tick de scroll: shapear (HarfBuzz) es lo caro, asi que se reusa el
    // FormattedText mientras la linea no cambie. Tope bajo porque cada entrada guarda glyph runs.
    static readonly Dictionary<(string, double, IBrush, bool), FormattedText> _cacheTextos = new();
    const int LimiteCacheTextos = 4_000;

    public static FormattedText Formatear(string texto, double fontSize, IBrush brush, bool negrita = false)
    {
        var clave = (texto, fontSize, brush, negrita);
        if (_cacheTextos.TryGetValue(clave, out var ft)) return ft;
        if (_cacheTextos.Count >= LimiteCacheTextos) _cacheTextos.Clear();
        return _cacheTextos[clave] = new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            negrita ? TypefaceNegrita : Typeface, fontSize, brush);
    }

    // anchos aparte (solo doubles): MeasureOverride recorre TODAS las lineas en cada tecla y no
    // debe vaciar el cache de textos de arriba
    static readonly Dictionary<(string, double), double> _cacheAnchos = new();
    const int LimiteCacheAnchos = 50_000;

    // incluye blancos finales (Width los recorta: una indentacion sola mediria 0)
    public static double MedirPrefijo(string texto, double fontSize)
    {
        if (texto.Length == 0) return 0;
        var clave = (texto, fontSize);
        if (_cacheAnchos.TryGetValue(clave, out double ancho)) return ancho;
        if (_cacheAnchos.Count >= LimiteCacheAnchos) _cacheAnchos.Clear();
        return _cacheAnchos[clave] = new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface, fontSize, Brushes.Black).WidthIncludingTrailingWhitespace;
    }

    // alto de linea derivado del tamaño de fuente (antes una constante de 20px fija a los 18px de
    // fuente originales, ver zoom en EditorTextArea/EditorGutter): un unico lugar para esta cuenta
    // asi el gutter y el area de texto nunca pueden desalinearse entre si al hacer zoom.
    public static double AltoLinea(double fontSize) => fontSize + 2;
}
