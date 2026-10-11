using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Controls;

// Minimapa con las lineas reales a fuente minuscula; siempre encoge el archivo entero al alto disponible (sin segundo scroll)
public class EditorMinimap : Control
{
    const double FontSizeMinimapa = 5;
    const double AltoLineaMaximo = 6;
    const double MargenIzquierdo = 2;

    public static readonly StyledProperty<EditorTabViewModel?> TabProperty =
        AvaloniaProperty.Register<EditorMinimap, EditorTabViewModel?>(nameof(Tab));

    public EditorTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    // Datos del ScrollViewer real; los sincroniza EditorView en cada ScrollChanged (no son bindeables)
    public static readonly StyledProperty<double> AltoTotalProperty =
        AvaloniaProperty.Register<EditorMinimap, double>(nameof(AltoTotal));

    public double AltoTotal
    {
        get => GetValue(AltoTotalProperty);
        set => SetValue(AltoTotalProperty, value);
    }

    public static readonly StyledProperty<double> AltoVisibleProperty =
        AvaloniaProperty.Register<EditorMinimap, double>(nameof(AltoVisible));

    public double AltoVisible
    {
        get => GetValue(AltoVisibleProperty);
        set => SetValue(AltoVisibleProperty, value);
    }

    public static readonly StyledProperty<double> DesplazamientoProperty =
        AvaloniaProperty.Register<EditorMinimap, double>(nameof(Desplazamiento));

    public double Desplazamiento
    {
        get => GetValue(DesplazamientoProperty);
        set => SetValue(DesplazamientoProperty, value);
    }

    // Emite el Offset.Y ya en coordenadas reales del editor
    public event Action<double>? DesplazamientoSolicitado;

    bool _arrastrando;

    public EditorMinimap()
    {
        ClipToBounds = true;
    }

    static EditorMinimap()
    {
        TabProperty.Changed.AddClassHandler<EditorMinimap>((c, e) =>
        {
            if (e.OldValue is EditorTabViewModel viejo) viejo.ContenidoCambiado -= c.InvalidateVisual;
            if (e.NewValue is EditorTabViewModel nuevo) nuevo.ContenidoCambiado += c.InvalidateVisual;
            c.InvalidateVisual();
        });

        // No se invalida el visual: estos valores solo los lee SolicitarSalto
    }

    public override void Render(DrawingContext context)
    {
        var fondo = (IBrush?)Application.Current?.FindResource("TemaCampoFondoBrush") ?? Brushes.WhiteSmoke;
        var texto = (IBrush?)Application.Current?.FindResource("TemaCampoTextoBrush") ?? Brushes.Black;

        context.FillRectangle(fondo, new Rect(Bounds.Size));
        if (Application.Current?.FindResource("TemaTitleBarBrush") is ISolidColorBrush acento)
            context.FillRectangle(new SolidColorBrush(acento.Color, 10.0 / 255), new Rect(Bounds.Size));

        if (Tab == null || Tab.Lineas.Count == 0) return;

        double altoLinea = Math.Min(AltoLineaMaximo, Bounds.Height / Tab.Lineas.Count);

        // Con mas lineas que pixeles se dibuja una de cada N
        int paso = Math.Max(1, (int)(1 / altoLinea));
        for (int i = 0; i < Tab.Lineas.Count; i += paso)
        {
            string linea = Tab.Lineas[i];
            if (linea.Length == 0) continue;

            double y = i * altoLinea;
            if (y > Bounds.Height) break;

            context.DrawText(EditorFont.Formatear(linea, FontSizeMinimapa, texto, negrita: true), new Point(MargenIzquierdo, y));
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        _arrastrando = true;
        e.Pointer.Capture(this);
        SolicitarSalto(e.GetPosition(this).Y);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_arrastrando) return;
        SolicitarSalto(e.GetPosition(this).Y);
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _arrastrando = false;
        e.Pointer.Capture(null);
    }

    void SolicitarSalto(double yEnMinimapa)
    {
        if (AltoTotal <= 0 || Bounds.Height <= 0) return;

        double fraccion = Math.Clamp(yEnMinimapa / Bounds.Height, 0, 1);
        double centroReal = fraccion * AltoTotal;
        double nuevoOffset = Math.Clamp(centroReal - AltoVisible / 2, 0, Math.Max(0, AltoTotal - AltoVisible));
        DesplazamientoSolicitado?.Invoke(nuevoOffset);
    }
}
