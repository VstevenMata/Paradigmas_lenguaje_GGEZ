using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Controls;

// Numeros de linea aparte de EditorTextArea para no desplazarse con el scroll horizontal
public class EditorGutter : Control
{
    const int MargenNumeros = 8;

    static readonly IBrush FondoGutter = new SolidColorBrush(Color.FromRgb(238, 238, 238));

    static readonly IBrush FondoGutterOscuro = new SolidColorBrush(Color.Parse("#161A24"));
    static readonly IBrush NumerosOscuro = new SolidColorBrush(Color.Parse("#5C6370"));

    public EditorGutter()
    {
        // Si un numero midiera mas de lo calculado, se recorta en vez de salirse del panel
        ClipToBounds = true;
    }

    // Se mide con el numero mas ancho posible (la ultima linea) al zoom actual; un ancho fijo se salia del panel
    double AnchoNumeros
    {
        get
        {
            int digitos = Math.Max(3, (Tab?.Lineas.Count ?? 0).ToString().Length);
            return EditorFont.MedirPrefijo(new string('9', digitos), TamanoFuente) + MargenNumeros;
        }
    }

    public static readonly StyledProperty<EditorTabViewModel?> TabProperty =
        AvaloniaProperty.Register<EditorGutter, EditorTabViewModel?>(nameof(Tab));

    public EditorTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    // Mismo calculo de AltoLinea que EditorTextArea para no desalinear los numeros con el zoom
    public static readonly StyledProperty<double> TamanoFuenteProperty =
        AvaloniaProperty.Register<EditorGutter, double>(nameof(TamanoFuente), 18.0);

    public double TamanoFuente
    {
        get => GetValue(TamanoFuenteProperty);
        set => SetValue(TamanoFuenteProperty, value);
    }

    public double AltoLinea => EditorFont.AltoLinea(TamanoFuente);

    public static readonly StyledProperty<bool> ModoOscuroProperty =
        AvaloniaProperty.Register<EditorGutter, bool>(nameof(ModoOscuro));

    public bool ModoOscuro
    {
        get => GetValue(ModoOscuroProperty);
        set => SetValue(ModoOscuroProperty, value);
    }

    // Sincronizados desde EditorView igual que en EditorTextArea: solo se dibujan las lineas visibles
    public static readonly StyledProperty<double> DesplazamientoYProperty =
        AvaloniaProperty.Register<EditorGutter, double>(nameof(DesplazamientoY));

    public double DesplazamientoY
    {
        get => GetValue(DesplazamientoYProperty);
        set => SetValue(DesplazamientoYProperty, value);
    }

    public static readonly StyledProperty<double> AltoVisibleProperty =
        AvaloniaProperty.Register<EditorGutter, double>(nameof(AltoVisible));

    public double AltoVisible
    {
        get => GetValue(AltoVisibleProperty);
        set => SetValue(AltoVisibleProperty, value);
    }

    static EditorGutter()
    {
        TabProperty.Changed.AddClassHandler<EditorGutter>((c, e) =>
        {
            if (e.OldValue is EditorTabViewModel viejo) viejo.ContenidoCambiado -= c.Refrescar;
            if (e.NewValue is EditorTabViewModel nuevo) nuevo.ContenidoCambiado += c.Refrescar;
            c.Refrescar();
        });

        TamanoFuenteProperty.Changed.AddClassHandler<EditorGutter>((c, _) => c.Refrescar());
        ModoOscuroProperty.Changed.AddClassHandler<EditorGutter>((c, _) => c.InvalidateVisual());

        DesplazamientoYProperty.Changed.AddClassHandler<EditorGutter>((c, _) => c.InvalidateVisual());
        AltoVisibleProperty.Changed.AddClassHandler<EditorGutter>((c, _) => c.InvalidateVisual());
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

        context.FillRectangle(ModoOscuro ? FondoGutterOscuro : FondoGutter, new Rect(Bounds.Size));
        var colorNumeros = ModoOscuro ? NumerosOscuro : Brushes.DarkGray;

        double anchoNumeros = AnchoNumeros;
        double margenDerecho = MargenNumeros / 2.0;
        double altoLinea = AltoLinea;

        int primeraLinea = 0;
        int ultimaLinea = Tab.Lineas.Count - 1;
        if (AltoVisible > 0)
        {
            primeraLinea = Math.Max(0, (int)(DesplazamientoY / altoLinea) - 2);
            ultimaLinea = Math.Min(Tab.Lineas.Count - 1, (int)((DesplazamientoY + AltoVisible) / altoLinea) + 2);
        }

        for (int i = primeraLinea; i <= ultimaLinea; i++)
        {
            string numero = (i + 1).ToString("D3");
            var formatted = EditorFont.Formatear(numero, TamanoFuente, colorNumeros);
            double y = i * altoLinea;
            context.DrawText(formatted, new Point(anchoNumeros - formatted.Width - margenDerecho, y));
        }
    }
}
