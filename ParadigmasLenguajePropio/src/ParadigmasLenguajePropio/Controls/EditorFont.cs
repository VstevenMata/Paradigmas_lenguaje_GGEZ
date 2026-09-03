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

    public static FormattedText Formatear(string texto, double fontSize, IBrush brush) =>
        new(texto, CultureInfo.CurrentCulture, FlowDirection.LeftToRight, Typeface, fontSize, brush);

    public static double Medir(string texto, double fontSize) =>
        texto.Length == 0 ? 0 : Formatear(texto, fontSize, Brushes.Black).Width;

    // Medir() recorta el avance de los blancos AL FINAL del texto (por eso una corrida de solo
    // espacios mide 0, ver AnchoContinuacion en EditorTextArea) - para medir un prefijo de linea
    // que puede terminar en blanco (indentacion, espacios sobrantes) se le pega un caracter no-blanco
    // al final y se resta su ancho, asi el blanco queda "en el medio" y si cuenta.
    public static double MedirPrefijo(string texto, double fontSize) =>
        texto.Length == 0 ? 0 : Medir(texto + "|", fontSize) - Medir("|", fontSize);
}
