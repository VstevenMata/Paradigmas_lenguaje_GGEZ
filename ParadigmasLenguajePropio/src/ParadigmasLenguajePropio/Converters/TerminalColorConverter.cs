using System.Globalization;
using Avalonia.Data.Converters;
using Avalonia.Media;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.Converters;

// misma paleta que PanelTerminal.cs (ColorPrompt/ColorEscrito/ColorError/ColorAviso)
public class TerminalColorConverter : IValueConverter
{
    public static readonly TerminalColorConverter Instance = new();

    static readonly IBrush Prompt = new SolidColorBrush(Color.FromRgb(0, 228, 48));
    static readonly IBrush Escrito = Brushes.White;
    static readonly IBrush Error = new SolidColorBrush(Color.FromRgb(255, 96, 96));
    static readonly IBrush Aviso = new SolidColorBrush(Color.FromRgb(255, 214, 96));

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) => value switch
    {
        TerminalColorLinea.Prompt => Prompt,
        TerminalColorLinea.Error => Error,
        TerminalColorLinea.Aviso => Aviso,
        _ => Escrito,
    };

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
