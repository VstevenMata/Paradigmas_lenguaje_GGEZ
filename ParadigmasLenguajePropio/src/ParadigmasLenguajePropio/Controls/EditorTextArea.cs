using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Controls;

// Reemplaza la mitad "texto" de PanelCentral.cs: dibujo via DrawingContext/FormattedText en vez de
// Raylib inmediato, hit-testing pixel->columna con FormattedText en vez de medir substring a mano,
// e IClipboard async en vez de Raylib.GetClipboardText_/SetClipboardText. El modelo de datos
// (lineas/cursor/seleccion/undo) vive intacto en EditorTabViewModel - esta clase es solo la
// capa de render + entrada.
public class EditorTextArea : Control
{
    public const int AltoLinea = 20;
    const int FontSize = 18;
    const int MargenIzquierdo = 4;

    static readonly IBrush ColorSeleccion = new SolidColorBrush(Color.FromArgb(110, 51, 153, 255));
    static readonly IBrush ColorLineaActual = new SolidColorBrush(Color.FromRgb(235, 235, 235));

    // ancho del resaltado de seleccion en una linea vacia (o al final de una linea que sigue en
    // la proxima): OJO, no se puede medir con EditorFont.Medir(" ", FontSize) - el ancho de
    // tinta de un espacio da 0 en este motor de texto (no cuenta el avance de blancos), lo que
    // dejaria el resaltado invisible de nuevo. Un valor fijo, mas grande que un caracter
    // angosto tipico, se ve como un bloque de verdad en vez de una raya de unos pocos px.
    const double AnchoContinuacion = 10;

    public static readonly StyledProperty<EditorTabViewModel?> TabProperty =
        AvaloniaProperty.Register<EditorTextArea, EditorTabViewModel?>(nameof(Tab));

    public EditorTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    // se dispara cuando el cursor se mueve, para que la vista contenedora haga scroll si hace falta
    public event Action? CursorMovido;

    bool _arrastrandoSeleccion;
    bool _caretVisible = true;
    readonly DispatcherTimer _parpadeo;

    public EditorTextArea()
    {
        Focusable = true;
        _parpadeo = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _parpadeo.Tick += (_, _) => { _caretVisible = !_caretVisible; if (IsFocused) InvalidateVisual(); };
        _parpadeo.Start();

        GotFocus += (_, _) => { _caretVisible = true; InvalidateVisual(); };
        LostFocus += (_, _) => InvalidateVisual();
    }

    static EditorTextArea()
    {
        TabProperty.Changed.AddClassHandler<EditorTextArea>((c, e) =>
        {
            if (e.OldValue is EditorTabViewModel viejo) { viejo.ContenidoCambiado -= c.Refrescar; viejo.CursorCambiado -= c.RefrescarCursor; }
            if (e.NewValue is EditorTabViewModel nuevo) { nuevo.ContenidoCambiado += c.Refrescar; nuevo.CursorCambiado += c.RefrescarCursor; }
            c.Refrescar();
        });
    }

    void Refrescar()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    void RefrescarCursor()
    {
        _caretVisible = true;
        InvalidateVisual();
        CursorMovido?.Invoke();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Tab == null) return default;
        double anchoMax = 0;
        foreach (var linea in Tab.Lineas)
            anchoMax = Math.Max(anchoMax, EditorFont.Medir(linea, FontSize));
        return new Size(anchoMax + MargenIzquierdo * 2 + 20, Tab.Lineas.Count * AltoLinea + 8);
    }

    public override void Render(DrawingContext context)
    {
        if (Tab == null) return;

        for (int i = 0; i < Tab.Lineas.Count; i++)
        {
            double y = 4 + i * AltoLinea;

            if (i == Tab.FilaCursor)
                context.FillRectangle(ColorLineaActual, new Rect(0, y, Bounds.Width, AltoLinea));

            DibujarSeleccionEnLinea(context, i, y);

            var texto = Tab.Lineas[i];
            if (texto.Length > 0)
                context.DrawText(EditorFont.Formatear(texto, FontSize, Brushes.Black), new Point(MargenIzquierdo, y));
        }

        DibujarCursor(context);
    }

    void DibujarSeleccionEnLinea(DrawingContext context, int indiceLinea, double y)
    {
        if (Tab == null || !Tab.HaySeleccion) return;
        var (fi, ci) = Tab.SeleccionDesde();
        var (ff, cf) = Tab.SeleccionHasta();
        if (indiceLinea < fi || indiceLinea > ff) return;

        string linea = Tab.Lineas[indiceLinea];
        int columnaInicio = indiceLinea == fi ? ci : 0;
        int columnaFin = indiceLinea == ff ? cf : linea.Length;

        double xInicio = EditorFont.Medir(linea[..columnaInicio], FontSize);
        double xFin = EditorFont.Medir(linea[..columnaFin], FontSize);
        // AnchoContinuacion: antes eran 6px fijos - en una linea vacia eso da un resaltado de
        // 6px, casi invisible contra el fondo blanco (parecia que no se podia seleccionar una
        // linea en blanco). Con el ancho de un espacio se nota como un bloque de verdad, igual
        // que en VSCode.
        if (indiceLinea != ff) xFin += AnchoContinuacion;

        var rect = new Rect(MargenIzquierdo + xInicio, y, Math.Max(AnchoContinuacion, xFin - xInicio), AltoLinea);
        context.FillRectangle(ColorSeleccion, rect);
    }

    // usado por EditorView para decidir si hace falta desplazar los ScrollViewer (AsegurarCursorVisible)
    public Rect ObtenerRectCursor()
    {
        if (Tab == null) return default;
        double x = MargenIzquierdo + EditorFont.Medir(Tab.Lineas[Tab.FilaCursor][..Tab.ColumnaCursor], FontSize);
        double y = 4 + Tab.FilaCursor * AltoLinea;
        return new Rect(x, y, 2, AltoLinea);
    }

    void DibujarCursor(DrawingContext context)
    {
        if (Tab == null || !IsFocused || !_caretVisible) return;

        double x = MargenIzquierdo + EditorFont.Medir(Tab.Lineas[Tab.FilaCursor][..Tab.ColumnaCursor], FontSize);
        double y = 4 + Tab.FilaCursor * AltoLinea;
        context.FillRectangle(Brushes.Black, new Rect(x, y, 2, FontSize));
    }

    // ─────────────────────────── mouse: click / arrastre para seleccionar ───────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (Tab == null || !e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) return;

        Focus();
        var (fila, columna) = PosicionEnPixel(e.GetPosition(this));
        Tab.MoverCursor(fila, columna, extenderSeleccion: e.KeyModifiers.HasFlag(KeyModifiers.Shift));
        Tab.ColumnaDeseada = columna;
        _arrastrandoSeleccion = true;
        e.Pointer.Capture(this);
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (Tab == null || !_arrastrandoSeleccion) return;

        var (fila, columna) = PosicionEnPixel(e.GetPosition(this));
        Tab.MoverCursor(fila, columna, extenderSeleccion: true);
        Tab.ColumnaDeseada = columna;
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        _arrastrandoSeleccion = false;
        e.Pointer.Capture(null);
    }

    (int fila, int columna) PosicionEnPixel(Point punto)
    {
        int fila = (int)((punto.Y - 4) / AltoLinea);
        fila = Math.Clamp(fila, 0, Tab!.Lineas.Count - 1);

        double xRelativo = punto.X - MargenIzquierdo;
        int columna = ColumnaEnX(Tab.Lineas[fila], xRelativo);
        return (fila, columna);
    }

    static int ColumnaEnX(string linea, double xRelativo)
    {
        double anchoPrevio = 0;
        for (int c = 0; c < linea.Length; c++)
        {
            double anchoActual = EditorFont.Medir(linea[..(c + 1)], FontSize);
            if (xRelativo < (anchoPrevio + anchoActual) / 2) return c;
            anchoPrevio = anchoActual;
        }
        return linea.Length;
    }

    // ─────────────────────────── teclado ───────────────────────────

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (Tab == null) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool alt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (ctrl && e.Key == Key.C) { Copiar(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.X) { Cortar(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.V) { _ = PegarAsync(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.A) { Tab.SeleccionarTodo(); e.Handled = true; return; }
        if (ctrl && shift && e.Key == Key.Z) { Tab.Rehacer(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.Z) { Tab.Deshacer(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.Y) { Tab.Rehacer(); e.Handled = true; return; }

        if (alt && e.Key == Key.Up) { Tab.MoverLineaArriba(); e.Handled = true; return; }
        if (alt && e.Key == Key.Down) { Tab.MoverLineaAbajo(); e.Handled = true; return; }

        if (e.Key == Key.Enter) { Tab.InsertarNuevaLinea(); e.Handled = true; return; }
        if (e.Key == Key.Tab) { Tab.InsertarTexto("    "); e.Handled = true; return; }
        if (e.Key == Key.Back) { Tab.Retroceder(); e.Handled = true; return; }
        if (e.Key == Key.Delete) { Tab.Suprimir(); e.Handled = true; return; }

        if (e.Key == Key.Left) { Tab.MoverIzquierda(shift); e.Handled = true; return; }
        if (e.Key == Key.Right) { Tab.MoverDerecha(shift); e.Handled = true; return; }
        if (e.Key == Key.Up) { Tab.MoverArriba(shift); e.Handled = true; return; }
        if (e.Key == Key.Down) { Tab.MoverAbajo(shift); e.Handled = true; return; }
        if (e.Key == Key.Home) { Tab.MoverInicioLinea(shift); e.Handled = true; return; }
        if (e.Key == Key.End) { Tab.MoverFinLinea(shift); e.Handled = true; return; }
    }

    // reemplaza el drenado de Raylib.GetCharPressed(): Avalonia entrega el texto ya compuesto
    // (funciona con IME/teclados muertos, GetCharPressed no)
    protected override void OnTextInput(TextInputEventArgs e)
    {
        base.OnTextInput(e);
        if (Tab == null || string.IsNullOrEmpty(e.Text)) return;
        Tab.InsertarTexto(e.Text);
        e.Handled = true;
    }

    void Copiar()
    {
        if (Tab is { HaySeleccion: true } t) _ = CopiarTextoAsync(t.TextoSeleccionado());
    }

    void Cortar()
    {
        if (Tab is not { HaySeleccion: true } t) return;
        _ = CopiarTextoAsync(t.TextoSeleccionado());
        t.BorrarSeleccion();
    }

    async Task PegarAsync()
    {
        var portapapeles = TopLevel.GetTopLevel(this)?.Clipboard;
        if (portapapeles == null) return;
        var texto = await portapapeles.TryGetTextAsync();
        if (!string.IsNullOrEmpty(texto)) Tab?.InsertarTexto(texto);
    }

    async Task CopiarTextoAsync(string texto)
    {
        var portapapeles = TopLevel.GetTopLevel(this)?.Clipboard;
        if (portapapeles != null) await portapapeles.SetTextAsync(texto);
    }
}
