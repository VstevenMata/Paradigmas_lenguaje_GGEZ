using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Controls;

// Reemplaza la mitad "números de línea" de PanelCentral.DibujarTexto: se dibujan aparte de
// EditorTextArea para poder vivir fuera de su ScrollViewer horizontal (los números no se
// desplazan de lado, solo verticalmente, junto con el texto).
public class EditorGutter : Control
{
    public const int AnchoNumeros = 40;
    public const int AltoLinea = 20;
    const int FontSize = 18;

    public static readonly StyledProperty<EditorTabViewModel?> TabProperty =
        AvaloniaProperty.Register<EditorGutter, EditorTabViewModel?>(nameof(Tab));

    public EditorTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    static EditorGutter()
    {
        TabProperty.Changed.AddClassHandler<EditorGutter>((c, e) =>
        {
            if (e.OldValue is EditorTabViewModel viejo) { viejo.ContenidoCambiado -= c.Refrescar; viejo.CursorCambiado -= c.Refrescar; }
            if (e.NewValue is EditorTabViewModel nuevo) { nuevo.ContenidoCambiado += c.Refrescar; nuevo.CursorCambiado += c.Refrescar; }
            c.Refrescar();
        });
    }

    void Refrescar()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(AnchoNumeros, (Tab?.Lineas.Count ?? 0) * AltoLinea);

    public override void Render(DrawingContext context)
    {
        if (Tab == null) return;

        for (int i = 0; i < Tab.Lineas.Count; i++)
        {
            string numero = (i + 1).ToString("D3");
            var formatted = EditorFont.Formatear(numero, FontSize, Brushes.DarkGray);
            double y = i * AltoLinea;
            context.DrawText(formatted, new Point(AnchoNumeros - formatted.Width - 4, y));
        }
    }
}
