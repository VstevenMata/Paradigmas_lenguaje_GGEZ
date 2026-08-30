using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;

namespace ParadigmasLenguajePropio.Controls;

// Reemplazo de Win95Style.DrawDivisorHorizontal/Vertical: linea oscura seguida de una clara, 1px cada una.
// Se dibuja directo (no via ControlTheme/Template) porque son dos lineas de 1px, no vale la pena
// templatizarlo por orientacion.
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

    // el tamaño deseado es solo su grosor: quien lo contiene lo estira (HorizontalAlignment/
    // VerticalAlignment por defecto son Stretch) via Arrange, no via Measure. Devolver
    // availableSize.Width/Height directo rompe con Grid.RowSpan/ColumnSpan, donde puede llegar
    // double.PositiveInfinity durante la medicion.
    protected override Size MeasureOverride(Size availableSize) =>
        Orientation == Orientation.Horizontal ? new Size(0, 2) : new Size(2, 0);

    public override void Render(DrawingContext context)
    {
        var dark = (IBrush?)Application.Current?.FindResource("Win95BevelDarkBrush") ?? Brushes.Gray;
        var light = (IBrush?)Application.Current?.FindResource("Win95BevelLightBrush") ?? Brushes.White;

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
