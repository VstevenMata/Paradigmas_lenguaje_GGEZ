using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Controls;

// Minimapa lateral (idea/minimapa.md): en vez de simular el texto con barras, dibuja las lineas
// reales a un tamaño de fuente minusculo - mas simple que sintetizar rectangulos por linea, y da
// una forma real del codigo (huecos de indentacion, lineas largas/cortas) en vez de una silueta
// aproximada.
//
// A diferencia de VSCode (que scrollea el minimapa junto con el editor cuando el archivo no entra
// completo), este SIEMPRE encoge el archivo entero para que quepa en el alto disponible - evita
// tener que implementar un segundo scroll sincronizado. El costo es que un archivo muy largo se
// ve mas comprimido/denso; se acepta ese trade-off por simplicidad (ver "Costo" en el .md).
public class EditorMinimap : Control
{
    const double FontSizeMinimapa = 5;
    const double AltoLineaMaximo = 6; // techo para que un archivo corto no estire el texto gigante
    const double MargenIzquierdo = 2;

    public static readonly StyledProperty<EditorTabViewModel?> TabProperty =
        AvaloniaProperty.Register<EditorMinimap, EditorTabViewModel?>(nameof(Tab));

    public EditorTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    // los tres datos del ScrollViewer real (PART_ScrollVertical en EditorView) que hacen falta
    // para dibujar el rectangulo de "que parte del archivo se esta viendo" y para saber a donde
    // saltar al hacer click/arrastrar aca - los sincroniza a mano EditorView.axaml.cs en cada
    // ScrollChanged, no son bindeables directo desde el XAML.
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

    // se dispara al hacer click o arrastrar en el minimapa: el nuevo Offset.Y que EditorView.axaml.cs
    // debe aplicarle a PART_ScrollVertical (ya viene calculado en coordenadas reales del editor,
    // no hace falta que quien escucha sepa nada de la escala del minimapa)
    public event Action<double>? DesplazamientoSolicitado;

    bool _arrastrando;

    public EditorMinimap()
    {
        ClipToBounds = true; // una linea larga no debe pintar fuera de esta columna angosta
    }

    static EditorMinimap()
    {
        TabProperty.Changed.AddClassHandler<EditorMinimap>((c, e) =>
        {
            if (e.OldValue is EditorTabViewModel viejo) viejo.ContenidoCambiado -= c.InvalidateVisual;
            if (e.NewValue is EditorTabViewModel nuevo) nuevo.ContenidoCambiado += c.InvalidateVisual;
            c.InvalidateVisual();
        });

        // AltoTotal/AltoVisible/Desplazamiento ya no se dibujan (ver Render): solo los lee
        // SolicitarSalto al hacer click/arrastrar, no hace falta invalidar el visual en cada
        // scroll por cambios que no afectan nada de lo que se pinta.
    }

    public override void Render(DrawingContext context)
    {
        var fondo = (IBrush?)Application.Current?.FindResource("TemaCampoFondoBrush") ?? Brushes.WhiteSmoke;
        var texto = (IBrush?)Application.Current?.FindResource("TemaCampoTextoBrush") ?? Brushes.Black;

        context.FillRectangle(fondo, new Rect(Bounds.Size));
        // un solo tono parejo del acento del tema activo en TODO el minimapa, sin distinguir la
        // porcion visible del resto - antes esa franja iba mas marcada y se notaba como dos
        // tonalidades distintas, cosa que no se queria. Alpha bien bajo: casi imperceptible a
        // proposito, un tinte apenas insinuado.
        if (Application.Current?.FindResource("TemaTitleBarBrush") is ISolidColorBrush acento)
            context.FillRectangle(new SolidColorBrush(acento.Color, 10.0 / 255), new Rect(Bounds.Size));

        if (Tab == null || Tab.Lineas.Count == 0) return;

        // encoger todo el archivo al alto disponible, con un techo para que uno corto no quede
        // con letras gigantes (ver comentario de clase)
        double altoLinea = Math.Min(AltoLineaMaximo, Bounds.Height / Tab.Lineas.Count);

        // con mas lineas que pixeles de alto se dibuja una de cada N: el resto caeria encimado
        int paso = Math.Max(1, (int)(1 / altoLinea));
        for (int i = 0; i < Tab.Lineas.Count; i += paso)
        {
            string linea = Tab.Lineas[i];
            if (linea.Length == 0) continue;

            double y = i * altoLinea;
            if (y > Bounds.Height) break; // el resto ya cae fuera de lo visible, no vale la pena seguir

            context.DrawText(EditorFont.Formatear(linea, FontSizeMinimapa, texto, negrita: true), new Point(MargenIzquierdo, y));
        }
    }

    // click o arrastre en el minimapa: centra el editor real en el punto clickeado, igual que
    // clickear en la scrollbar de VSCode
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
