using System.ComponentModel;
using System.Text.RegularExpressions;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using ParadigmasLenguajePropio.Models;
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

    // paletas de las extensiones de resaltado (ver DibujarGuiaIndentacion/DibujarBrackets) - los
    // colores no se toman de ExtensionInfo.IconoColor (esa es la placa de un solo color del panel
    // de extensiones): aca hace falta una secuencia de varios colores, uno por nivel de anidado
    static readonly IBrush[] PaletaIndentacion =
    {
        new SolidColorBrush(Color.Parse("#E06C75")), new SolidColorBrush(Color.Parse("#61AFEF")),
        new SolidColorBrush(Color.Parse("#98C379")), new SolidColorBrush(Color.Parse("#C678DD")),
        new SolidColorBrush(Color.Parse("#E5C07B")),
    };
    static readonly IBrush[] PaletaBrackets =
    {
        new SolidColorBrush(Color.Parse("#DAA520")), new SolidColorBrush(Color.Parse("#DA70D6")),
        new SolidColorBrush(Color.Parse("#179FFF")),
    };
    static readonly IBrush ColorEspaciosSobrantes = new SolidColorBrush(Color.FromArgb(90, 239, 68, 68));
    static readonly (string Palabra, IBrush Color)[] PalabrasTodo =
    {
        ("TODO", new SolidColorBrush(Color.FromArgb(90, 59, 130, 246))),
        ("FIXME", new SolidColorBrush(Color.FromArgb(90, 239, 68, 68))),
        ("HACK", new SolidColorBrush(Color.FromArgb(90, 245, 158, 11))),
    };
    static readonly Regex PatronHex = new(@"#(?:[0-9A-Fa-f]{6}|[0-9A-Fa-f]{3})\b", RegexOptions.Compiled);
    static readonly Regex PatronRgb = new(@"rgba?\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})[^)]*\)", RegexOptions.Compiled);

    public static readonly StyledProperty<EditorTabViewModel?> TabProperty =
        AvaloniaProperty.Register<EditorTextArea, EditorTabViewModel?>(nameof(Tab));

    public EditorTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    // extensiones de resaltado (ver ExtensionsViewModel): rainbow-indent, trailing-whitespace,
    // color-lens, todo-highlight y bracket-colorizer se consultan en cada Render() (ver mas
    // abajo) - null mientras la vista de diseño no tiene DataContext real
    public static readonly StyledProperty<ExtensionsViewModel?> ExtensionesProperty =
        AvaloniaProperty.Register<EditorTextArea, ExtensionsViewModel?>(nameof(Extensiones));

    public ExtensionsViewModel? Extensiones
    {
        get => GetValue(ExtensionesProperty);
        set => SetValue(ExtensionesProperty, value);
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

        // instalar/desinstalar una extension desde el panel de Extensiones debe repintar de
        // inmediato, sin esperar a que el usuario tipee o mueva el cursor
        ExtensionesProperty.Changed.AddClassHandler<EditorTextArea>((c, e) =>
        {
            if (e.OldValue is ExtensionsViewModel viejo) foreach (var ext in ExtensionesDeResaltado(viejo)) ext.PropertyChanged -= c.ExtensionInstalada_PropertyChanged;
            if (e.NewValue is ExtensionsViewModel nuevo) foreach (var ext in ExtensionesDeResaltado(nuevo)) ext.PropertyChanged += c.ExtensionInstalada_PropertyChanged;
            c.Refrescar();
        });
    }

    static IEnumerable<ExtensionInfo> ExtensionesDeResaltado(ExtensionsViewModel ext) =>
        new[] { ext.RainbowIndent, ext.TrailingWhitespace, ext.ColorLens, ext.TodoHighlight, ext.BracketColorizer };

    void ExtensionInstalada_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ExtensionInfo.Instalada)) Refrescar();
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
            anchoMax = Math.Max(anchoMax, EditorFont.MedirPrefijo(linea, FontSize));
        return new Size(anchoMax + MargenIzquierdo * 2 + 20, Tab.Lineas.Count * AltoLinea + 8);
    }

    public override void Render(DrawingContext context)
    {
        if (Tab == null) return;

        // sin esto, un Control sin Background no tiene nada "pintado" en las zonas donde no se
        // dibuja texto/resaltado (ej. una linea vacia que no es la actual) - y ahi el click
        // atraviesa el control (lo agarra el ScrollContentPresenter de atras) en vez de llegarle a
        // EditorTextArea. Blanco porque coincide con el fondo del Win95Bevel que lo envuelve
        // (EditorView.axaml), asi que no cambia nada visualmente.
        context.FillRectangle(Brushes.White, new Rect(Bounds.Size));

        bool rainbowIndent = Extensiones?.RainbowIndent.Instalada == true;
        bool espaciosSobrantes = Extensiones?.TrailingWhitespace.Instalada == true;
        bool colorLens = Extensiones?.ColorLens.Instalada == true;
        bool todoHighlight = Extensiones?.TodoHighlight.Instalada == true;
        bool bracketColorizer = Extensiones?.BracketColorizer.Instalada == true;

        // nivel de anidado de brackets: se acumula linea a linea (un bracket puede abrir en una
        // linea y cerrar varias mas abajo), por eso vive afuera del for en vez de reiniciarse por linea
        int nivelBrackets = 0;

        for (int i = 0; i < Tab.Lineas.Count; i++)
        {
            double y = 4 + i * AltoLinea;
            var texto = Tab.Lineas[i];

            if (i == Tab.FilaCursor)
                context.FillRectangle(ColorLineaActual, new Rect(0, y, Bounds.Width, AltoLinea));

            DibujarSeleccionEnLinea(context, i, y);

            // resaltados de fondo: van ANTES del texto para no taparlo. Los espacios sobrantes van
            // primero y las guias de indentacion despues: si una linea es solo espacios, las dos
            // se superponen - con las guias arriba siguen viendose sus colores en vez de quedar
            // lavadas bajo el rosa translucido del trim
            if (espaciosSobrantes) DibujarEspaciosSobrantes(context, texto, y);
            if (rainbowIndent) DibujarGuiaIndentacion(context, texto, y);
            if (todoHighlight) DibujarTodoHighlight(context, texto, y);

            if (texto.Length > 0)
                context.DrawText(EditorFont.Formatear(texto, FontSize, Brushes.Black), new Point(MargenIzquierdo, y));

            // recoloreados/anotaciones: van DESPUES del texto, se dibujan encima
            if (bracketColorizer) nivelBrackets = DibujarBrackets(context, texto, y, nivelBrackets);
            if (colorLens) DibujarColorLens(context, texto, y);
        }

        DibujarCursor(context);
    }

    // ─────────────────────────── extension: Rainbow Indent Guides ───────────────────────────

    static void DibujarGuiaIndentacion(DrawingContext context, string texto, double y)
    {
        int espacios = 0;
        while (espacios < texto.Length && texto[espacios] == ' ') espacios++;

        int niveles = espacios / 4;
        for (int nivel = 0; nivel < niveles; nivel++)
        {
            double x = MargenIzquierdo + EditorFont.MedirPrefijo(new string(' ', nivel * 4), FontSize);
            var pluma = new Pen(PaletaIndentacion[nivel % PaletaIndentacion.Length], 1);
            context.DrawLine(pluma, new Point(x, y), new Point(x, y + AltoLinea));
        }
    }

    // ─────────────────────────── extension: Trim Trailing Whitespace ───────────────────────────

    static void DibujarEspaciosSobrantes(DrawingContext context, string texto, double y)
    {
        int inicio = texto.Length;
        while (inicio > 0 && texto[inicio - 1] == ' ') inicio--;
        if (inicio == texto.Length) return; // sin espacios sobrantes

        double xInicio = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..inicio], FontSize);
        double xFin = MargenIzquierdo + EditorFont.MedirPrefijo(texto, FontSize);
        var rect = new Rect(xInicio, y, Math.Max(AnchoContinuacion, xFin - xInicio), AltoLinea);
        context.FillRectangle(ColorEspaciosSobrantes, rect);
    }

    // ─────────────────────────── extension: TODO Highlight ───────────────────────────

    static void DibujarTodoHighlight(DrawingContext context, string texto, double y)
    {
        foreach (var (palabra, color) in PalabrasTodo)
        {
            int idx = 0;
            while ((idx = texto.IndexOf(palabra, idx, StringComparison.Ordinal)) >= 0)
            {
                double xInicio = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..idx], FontSize);
                double xFin = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..(idx + palabra.Length)], FontSize);
                context.FillRectangle(color, new Rect(xInicio, y, Math.Max(AnchoContinuacion, xFin - xInicio), AltoLinea));
                idx += palabra.Length;
            }
        }
    }

    // ─────────────────────────── extension: Bracket Pair Colorizer ───────────────────────────

    static int DibujarBrackets(DrawingContext context, string texto, double y, int nivel)
    {
        for (int c = 0; c < texto.Length; c++)
        {
            char ch = texto[c];
            bool esApertura = ch is '(' or '[' or '{';
            bool esCierre = ch is ')' or ']' or '}';
            if (!esApertura && !esCierre) continue;

            if (esCierre) nivel = Math.Max(0, nivel - 1);

            double x = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..c], FontSize);
            var brush = PaletaBrackets[nivel % PaletaBrackets.Length];
            context.DrawText(EditorFont.Formatear(ch.ToString(), FontSize, brush), new Point(x, y));

            if (esApertura) nivel++;
        }
        return nivel;
    }

    // ─────────────────────────── extension: Color Lens ───────────────────────────

    static void DibujarColorLens(DrawingContext context, string texto, double y)
    {
        foreach (Match m in PatronHex.Matches(texto))
        {
            if (TryParseColorHex(m.Value, out var color)) DibujarSwatch(context, texto, m.Index + m.Length, y, color);
        }
        foreach (Match m in PatronRgb.Matches(texto))
        {
            if (byte.TryParse(m.Groups[1].Value, out var r) && byte.TryParse(m.Groups[2].Value, out var g) && byte.TryParse(m.Groups[3].Value, out var b))
                DibujarSwatch(context, texto, m.Index + m.Length, y, Color.FromRgb(r, g, b));
        }
    }

    static void DibujarSwatch(DrawingContext context, string texto, int columnaFin, double y, Color color)
    {
        double x = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..columnaFin], FontSize) + 3;
        var rect = new Rect(x, y + 4, 12, 12);
        context.FillRectangle(new SolidColorBrush(color), rect);
        context.DrawRectangle(new Pen(Brushes.Black, 1), rect);
    }

    static bool TryParseColorHex(string texto, out Color color)
    {
        try { color = Color.Parse(texto); return true; }
        catch (FormatException) { color = default; return false; }
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

        double xInicio = EditorFont.MedirPrefijo(linea[..columnaInicio], FontSize);
        double xFin = EditorFont.MedirPrefijo(linea[..columnaFin], FontSize);
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
        double x = MargenIzquierdo + EditorFont.MedirPrefijo(Tab.Lineas[Tab.FilaCursor][..Tab.ColumnaCursor], FontSize);
        double y = 4 + Tab.FilaCursor * AltoLinea;
        return new Rect(x, y, 2, AltoLinea);
    }

    void DibujarCursor(DrawingContext context)
    {
        if (Tab == null || !IsFocused || !_caretVisible) return;

        double x = MargenIzquierdo + EditorFont.MedirPrefijo(Tab.Lineas[Tab.FilaCursor][..Tab.ColumnaCursor], FontSize);
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
            // MedirPrefijo (no Medir): un prefijo que termina en blanco (ej. clickear dentro de la
            // indentacion, o en una linea que es solo espacios) mide 0 con Medir - el click
            // colapsaba siempre al final de la linea en vez de a la columna real
            double anchoActual = EditorFont.MedirPrefijo(linea[..(c + 1)], FontSize);
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
