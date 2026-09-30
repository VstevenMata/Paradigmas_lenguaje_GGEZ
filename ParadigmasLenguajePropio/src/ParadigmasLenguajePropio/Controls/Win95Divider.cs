using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ParadigmasLenguajePropio.Controls;

// Divisor Win95: linea oscura seguida de una clara, 1px cada una
public class Win95Divider : Control
{
    public static readonly StyledProperty<Orientation> OrientationProperty =
        AvaloniaProperty.Register<Win95Divider, Orientation>(nameof(Orientation), Orientation.Horizontal);

    public Orientation Orientation
    {
        get => GetValue(OrientationProperty);
        set => SetValue(OrientationProperty, value);
    }

    static Win95Divider()
    {
        AffectsRender<Win95Divider>(OrientationProperty);
    }

    // Devolver availableSize rompe con RowSpan/ColumnSpan (llega Infinity); el contenedor lo estira en Arrange
    protected override Size MeasureOverride(Size availableSize) =>
        Orientation == Orientation.Horizontal ? new Size(0, 2) : new Size(2, 0);

    public override void Render(DrawingContext context)
    {
        var dark = (IBrush?)Application.Current?.FindResource("TemaBevelDarkBrush") ?? Brushes.Gray;
        var light = (IBrush?)Application.Current?.FindResource("TemaBevelLightBrush") ?? Brushes.White;

        if (Orientation == Orientation.Horizontal)
        {
            context.FillRectangle(dark, new Rect(0, 0, Bounds.Width, 1));
            context.FillRectangle(light, new Rect(0, 1, Bounds.Width, 1));
        }
        else
        {
            context.FillRectangle(dark, new Rect(0, 0, 1, Bounds.Height));
            context.FillRectangle(light, new Rect(1, 0, 1, Bounds.Height));
        }
    }
}
