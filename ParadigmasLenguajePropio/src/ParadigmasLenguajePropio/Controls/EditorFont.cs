using System.Collections.Generic;
using System.Globalization;
using Avalonia.Media;

namespace ParadigmasLenguajePropio.Controls;

// Tipografia compartida del editor con cache de shaping
static class EditorFont
{
    public static readonly Typeface Typeface = new(FontFamily.Parse(
        "avares://ParadigmasLenguajePropio/Assets/Fonts/LiberationSans-Regular.ttf#Liberation Sans"));

    // No hay .ttf bold: Skia emboldece la misma familia de forma sintetica
    public static readonly Typeface TypefaceNegrita = new(FontFamily.Parse(
        "avares://ParadigmasLenguajePropio/Assets/Fonts/LiberationSans-Regular.ttf#Liberation Sans"),
        FontStyle.Normal, FontWeight.Bold);

    // Shapear (HarfBuzz) es lo caro: se reusa el FormattedText mientras la linea no cambie
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

    // Cache aparte: MeasureOverride recorre todas las lineas en cada tecla y no debe vaciar el de textos
    static readonly Dictionary<(string, double), double> _cacheAnchos = new();
    const int LimiteCacheAnchos = 50_000;

    // Incluye blancos finales (Width los recorta: una indentacion sola mediria 0)
    public static double MedirPrefijo(string texto, double fontSize)
    {
        if (texto.Length == 0) return 0;
        var clave = (texto, fontSize);
        if (_cacheAnchos.TryGetValue(clave, out double ancho)) return ancho;
        if (_cacheAnchos.Count >= LimiteCacheAnchos) _cacheAnchos.Clear();
        return _cacheAnchos[clave] = new FormattedText(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight,
            Typeface, fontSize, Brushes.Black).WidthIncludingTrailingWhitespace;
    }

    // Unico lugar de esta cuenta para que gutter y texto no se desalineen con el zoom
    public static double AltoLinea(double fontSize) => fontSize + 2;
}
