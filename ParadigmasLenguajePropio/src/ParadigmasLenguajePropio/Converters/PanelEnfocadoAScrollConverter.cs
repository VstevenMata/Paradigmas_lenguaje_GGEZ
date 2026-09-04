using System.Globalization;
using Avalonia.Controls.Primitives;
using Avalonia.Data.Converters;

namespace ParadigmasLenguajePropio.Converters;

// con varios paneles abiertos (split), solo el panel enfocado muestra minimapa + ScrollBar propio
// (ver EditorView.axaml/EsPanelEnfocado) - los paneles sin foco vuelven a mostrar la scrollbar
// nativa del ScrollViewer, para no quedarse sin forma de scrollear con el mouse en esos paneles.
public class PanelEnfocadoAScrollConverter : IValueConverter
{
    public static readonly PanelEnfocadoAScrollConverter Instance = new();

    public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        value is true ? ScrollBarVisibility.Hidden : ScrollBarVisibility.Auto;

    public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}
