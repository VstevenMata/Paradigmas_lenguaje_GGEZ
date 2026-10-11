using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Threading;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Controls;

// Capa de render + entrada del editor; el modelo (lineas/cursor/undo) vive en EditorTabViewModel
public class EditorTextArea : Control
{
    const int MargenIzquierdo = 4;

    // AltoLinea sale de TamanoFuente con el mismo calculo que EditorGutter para no desalinear los numeros
    public static readonly StyledProperty<double> TamanoFuenteProperty =
        AvaloniaProperty.Register<EditorTextArea, double>(nameof(TamanoFuente), 18.0);

    public double TamanoFuente
    {
        get => GetValue(TamanoFuenteProperty);
        set => SetValue(TamanoFuenteProperty, value);
    }

    public double AltoLinea => EditorFont.AltoLinea(TamanoFuente);

    // Los sincroniza EditorView en cada ScrollChanged para que Render() dibuje solo las lineas visibles
    public static readonly StyledProperty<double> DesplazamientoYProperty =
        AvaloniaProperty.Register<EditorTextArea, double>(nameof(DesplazamientoY));

    public double DesplazamientoY
    {
        get => GetValue(DesplazamientoYProperty);
        set => SetValue(DesplazamientoYProperty, value);
    }

    public static readonly StyledProperty<double> AltoVisibleProperty =
        AvaloniaProperty.Register<EditorTextArea, double>(nameof(AltoVisible));

    public double AltoVisible
    {
        get => GetValue(AltoVisibleProperty);
        set => SetValue(AltoVisibleProperty, value);
    }

    public static readonly StyledProperty<int> TamanoTabProperty =
        AvaloniaProperty.Register<EditorTextArea, int>(nameof(TamanoTab), 4);

    public int TamanoTab
    {
        get => GetValue(TamanoTabProperty);
        set => SetValue(TamanoTabProperty, value);
    }

    static readonly IBrush ColorSeleccion = new SolidColorBrush(Color.FromArgb(110, 51, 153, 255));
    static readonly IBrush ColorLineaActual = new SolidColorBrush(Color.FromRgb(235, 235, 235));

    public static readonly IBrush FondoOscuro = new SolidColorBrush(Color.Parse("#0F1117"));
    public static readonly IBrush TextoOscuro = new SolidColorBrush(Color.Parse("#D4D4D4"));
    static readonly IBrush LineaActualOscura = new SolidColorBrush(Color.Parse("#1C2030"));

    const double AnchoContinuacion = 10;

    // Paleta compartida por Rainbow Indent y Bracket Colorizer: mismo nivel de anidado, mismo color
    static readonly IBrush[] PaletaAnidado =
    {
        new SolidColorBrush(Color.Parse("#D82800")),
        new SolidColorBrush(Color.Parse("#5C94FC")),
        new SolidColorBrush(Color.Parse("#FCE000")),
        new SolidColorBrush(Color.Parse("#00A800")),
        new SolidColorBrush(Color.Parse("#FCA044")),
        new SolidColorBrush(Color.Parse("#7C1EFC")),
        new SolidColorBrush(Color.Parse("#00E8D8")),
        new SolidColorBrush(Color.Parse("#F878F8")),
        new SolidColorBrush(Color.Parse("#F87800")),
        new SolidColorBrush(Color.Parse("#005800")),
        new SolidColorBrush(Color.Parse("#0000BC")),
        new SolidColorBrush(Color.Parse("#58F898")),
        new SolidColorBrush(Color.Parse("#FCBC00")),
        new SolidColorBrush(Color.Parse("#940000")),
        new SolidColorBrush(Color.Parse("#005850")),
    };
    static readonly IPen[] PlumasAnidado = PaletaAnidado.Select(b => (IPen)new Pen(b, 1)).ToArray();
    static readonly IBrush ColorEspaciosSobrantes = new SolidColorBrush(Color.FromArgb(90, 239, 68, 68));
    static readonly (string Palabra, IBrush Color)[] PalabrasTodo =
    {
        ("TODO", new SolidColorBrush(Color.FromArgb(90, 59, 130, 246))),
        ("FIXME", new SolidColorBrush(Color.FromArgb(90, 239, 68, 68))),
        ("HACK", new SolidColorBrush(Color.FromArgb(90, 245, 158, 11))),
    };

    /*Los colores de las variables estan basados en los tipos de rarezas que 
    tienen los items en los videojuegos*/
    static readonly IBrush ColorTipoDato = new SolidColorBrush(Color.Parse("#4FA3FF"));
    static readonly IBrush EstructuraDelPrograma = new SolidColorBrush(Color.Parse("#2EC4B6"));
    static readonly IBrush Declaraciones = new SolidColorBrush(Color.Parse("#C792EA"));
    static readonly IBrush Modificadores = new SolidColorBrush(Color.Parse("#F07178"));
    static readonly IBrush FlujoDeControl = new SolidColorBrush(Color.Parse("#5C7CFA"));
    static readonly IBrush ManejoDeErrores = new SolidColorBrush(Color.Parse("#FFD23F"));
    static readonly IBrush RojoDano = new SolidColorBrush(Color.Parse("#FF4D4D"));
    static readonly IBrush VerdeVida = new SolidColorBrush(Color.Parse("#3DDC84"));
    static readonly IBrush Nulo = new SolidColorBrush(Color.FromArgb(179, 0x6B, 0x6B, 0x6B));
    static readonly IBrush Instancia = new SolidColorBrush(Color.Parse("#7FDBFF"));
    static readonly IBrush Entrada = new SolidColorBrush(Color.Parse("#00E5FF"));
    static readonly IBrush Suma = new SolidColorBrush(Color.Parse("#7EE787"));
    static readonly IBrush Resta = new SolidColorBrush(Color.Parse("#FF7B72"));
    static readonly IBrush Critico = new SolidColorBrush(Color.Parse("#FFE66D"));
    static readonly IBrush Reparto = new SolidColorBrush(Color.Parse("#98C1D9"));
    static readonly IBrush Relacionales = new SolidColorBrush(Color.Parse("#E5C07B"));
    static readonly IBrush Delimitadores = new SolidColorBrush(Color.Parse("#5C6370"));
    static readonly IBrush Asignacion = new SolidColorBrush(Color.Parse("#ABB2BF"));
    static readonly Dictionary<string, IBrush> ColoresSintaxis = new()
    {
        ["lobby"] = EstructuraDelPrograma,  
        ["launch"] = EstructuraDelPrograma,
        ["start"] = EstructuraDelPrograma,  
        ["THE"] = EstructuraDelPrograma,
        ["GAME"] = EstructuraDelPrograma,
        ["travel"] = EstructuraDelPrograma, 
        ["from"] = EstructuraDelPrograma,   
        ["server"] = EstructuraDelPrograma, 
        ["region"] = EstructuraDelPrograma, 
        ["quit"] = RojoDano,                

        ["stash"] = Declaraciones,     
        ["hardcoded"] = Declaraciones, 
        ["noloot"] = ColorTipoDato,    
        ["afk"] = Nulo,                

        ["score"] = ColorTipoDato,
        ["aim"] = ColorTipoDato, 
        ["lore"] = ColorTipoDato,
        ["win"] = ColorTipoDato, 
        ["pixel"] = ColorTipoDato, 
        ["squad"] = ColorTipoDato,
        ["stack"] = ColorTipoDato, 
        ["matchmaking"] = ColorTipoDato,
        ["inventory"] = ColorTipoDato, 

        ["hit"] = VerdeVida, 
        ["miss"] = RojoDano, 

        ["scan"] = FlujoDeControl,
        ["fallback"] = FlujoDeControl, 
        ["farm"] = FlujoDeControl,
        ["survive"] = FlujoDeControl,
        ["grind"] = FlujoDeControl, 
        ["loadout"] = FlujoDeControl,
        ["pick"] = FlujoDeControl,
        ["noob"] = FlujoDeControl, 
        ["ragequit"] = FlujoDeControl,
        ["respawn"] = FlujoDeControl,

        ["combo"] = Declaraciones,
        ["drops"] = Declaraciones, 
        ["reward"] = Declaraciones, 

        ["clutch"] = ManejoDeErrores,
        ["revive"] = ManejoDeErrores, 
        ["checkpoint"] = ManejoDeErrores, 
        ["wipe"] = RojoDano, 

        ["guild"] = Declaraciones,
        ["questline"] = Declaraciones, 
        ["evolves"] = Declaraciones,
        ["spawn"] = Declaraciones,
        ["me"] = Instancia, 
        ["open"] = Modificadores,
        ["party"] = Modificadores, 
        ["solo"] = Modificadores, 

        ["load"] = Entrada,
        ["rage"] = RojoDano,
        ["physics"] = EstructuraDelPrograma, 
        ["rng"] = EstructuraDelPrograma,
        ["clock"] = EstructuraDelPrograma, 
        ["save"] = EstructuraDelPrograma,

        ["buff"] = Suma,
        ["nerf"] = Resta,

        ["glhf"] = Delimitadores, 
        ["gg"] = Delimitadores,

        ["gain"] = Suma, 
        ["drain"] = Resta, 
        ["crit"] = Critico, 
        ["split"] = Reparto,
        ["scrap"] = Reparto,

        ["matches"] = Relacionales, 
        ["mismatch"] = Relacionales,
        ["over"] = Relacionales, 
        ["under"] = Relacionales, 
        ["atleast"] = Relacionales, 
        ["atmost"] = Relacionales, 

        ["set"] = Asignacion, 
        ["boost"] = Suma, 
        ["sap"] = Resta,
    };

    public static readonly StyledProperty<EditorTabViewModel?> TabProperty =
        AvaloniaProperty.Register<EditorTextArea, EditorTabViewModel?>(nameof(Tab));

    public EditorTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    public static readonly StyledProperty<ExtensionsViewModel?> ExtensionesProperty =
        AvaloniaProperty.Register<EditorTextArea, ExtensionsViewModel?>(nameof(Extensiones));

    public ExtensionsViewModel? Extensiones
    {
        get => GetValue(ExtensionesProperty);
        set => SetValue(ExtensionesProperty, value);
    }

    public event Action? CursorMovido;

    // 1 = acercar, -1 = alejar, 0 = restablecer; el zoom real lo guarda el ViewModel
    public event Action<int>? ZoomSolicitado;

    bool _arrastrandoSeleccion;
    bool _caretVisible = true;
    readonly DispatcherTimer _parpadeo;

    public EditorTextArea()
    {
        Focusable = true;
        _parpadeo = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(500) };
        _parpadeo.Tick += (_, _) => { _caretVisible = !_caretVisible; InvalidateVisual(); };

        GotFocus += (_, _) => { _caretVisible = true; _parpadeo.Start(); InvalidateVisual(); };
        LostFocus += (_, _) => { _parpadeo.Stop(); InvalidateVisual(); };
    }

    static EditorTextArea()
    {
        TabProperty.Changed.AddClassHandler<EditorTextArea>((c, e) =>
        {
            if (e.OldValue is EditorTabViewModel viejo) { viejo.ContenidoCambiado -= c.Refrescar; viejo.CursorCambiado -= c.RefrescarCursor; }
            if (e.NewValue is EditorTabViewModel nuevo) { nuevo.ContenidoCambiado += c.Refrescar; nuevo.CursorCambiado += c.RefrescarCursor; }
            c.Refrescar();
        });

        ExtensionesProperty.Changed.AddClassHandler<EditorTextArea>((c, e) =>
        {
            if (e.OldValue is ExtensionsViewModel viejo) foreach (var ext in ExtensionesDeResaltado(viejo)) ext.PropertyChanged -= c.ExtensionInstalada_PropertyChanged;
            if (e.NewValue is ExtensionsViewModel nuevo) foreach (var ext in ExtensionesDeResaltado(nuevo)) ext.PropertyChanged += c.ExtensionInstalada_PropertyChanged;
            c.Refrescar();
        });

        // Refrescar() y no solo InvalidateVisual(): el zoom cambia el ancho medido de cada linea
        TamanoFuenteProperty.Changed.AddClassHandler<EditorTextArea>((c, _) => c.Refrescar());

        DesplazamientoYProperty.Changed.AddClassHandler<EditorTextArea>((c, _) => c.InvalidateVisual());
        AltoVisibleProperty.Changed.AddClassHandler<EditorTextArea>((c, _) => c.InvalidateVisual());
    }

    static IEnumerable<ExtensionInfo> ExtensionesDeResaltado(ExtensionsViewModel ext) =>
        new[] { ext.RainbowIndent, ext.TrailingWhitespace, ext.TodoHighlight, ext.BracketColorizer, ext.ModoOscuro, ext.PacManBloques };

    bool ModoOscuro => Extensiones?.ModoOscuro.Instalada == true;
    IBrush ColorTexto => ModoOscuro ? TextoOscuro : Brushes.Black;

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
        if (_parpadeo.IsEnabled) { _parpadeo.Stop(); _parpadeo.Start(); }
        InvalidateVisual();
        CursorMovido?.Invoke();
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        if (Tab == null) return default;
        double anchoMax = 0;
        foreach (var linea in Tab.Lineas)
            anchoMax = Math.Max(anchoMax, EditorFont.MedirPrefijo(linea, TamanoFuente));
        return new Size(anchoMax + MargenIzquierdo * 2 + 20, Tab.Lineas.Count * AltoLinea + 8);
    }

    public override void Render(DrawingContext context)
    {
        if (Tab == null) return;

        // Sin fondo pintado, el click en zonas vacias atraviesa el control y no llega al editor
        bool modoOscuro = ModoOscuro;
        var colorTexto = ColorTexto;
        context.FillRectangle(modoOscuro ? FondoOscuro : Brushes.White, new Rect(Bounds.Size));

        bool rainbowIndent = Extensiones?.RainbowIndent.Instalada == true;
        bool espaciosSobrantes = Extensiones?.TrailingWhitespace.Instalada == true;
        bool todoHighlight = Extensiones?.TodoHighlight.Instalada == true;
        bool bracketColorizer = Extensiones?.BracketColorizer.Instalada == true;
        bool pacManBloques = Extensiones?.PacManBloques.Instalada == true;
        double fontSize = TamanoFuente;
        double altoLinea = AltoLinea;

        // Si aun no llego el primer ScrollChanged (AltoVisible = 0) se dibuja todo el documento
        int primeraLinea = 0;
        int ultimaLinea = Tab.Lineas.Count - 1;
        if (AltoVisible > 0)
        {
            primeraLinea = Math.Max(0, (int)(DesplazamientoY / altoLinea) - 2);
            ultimaLinea = Math.Min(Tab.Lineas.Count - 1, (int)((DesplazamientoY + AltoVisible) / altoLinea) + 2);
        }

        // Nivel de brackets y estado de comentario de bloque se arrastran desde las lineas previas al viewport
        int nivelBrackets = 0;
        bool enComentarioBloque = false;
        for (int i = 0; i < primeraLinea; i++)
        {
            var comentariosPrevios = BuscarComentarios(Tab.Lineas[i], ref enComentarioBloque);
            nivelBrackets = ProcesarBrackets(context, Tab.Lineas[i], y: 0, nivelBrackets, fontSize, dibujar: false, comentarios: comentariosPrevios);
        }

        var colorComentario = modoOscuro ? ComentarioOscuro : ComentarioClaro;

        for (int i = primeraLinea; i <= ultimaLinea; i++)
        {
            double y = 4 + i * altoLinea;
            var texto = Tab.Lineas[i];

            if (i == Tab.FilaCursor)
                context.FillRectangle(modoOscuro ? LineaActualOscura : ColorLineaActual, new Rect(0, y, Bounds.Width, altoLinea));

            DibujarSeleccionEnLinea(context, i, y);

            // Fondos antes del texto; guias despues de espacios sobrantes para que no queden lavadas
            if (espaciosSobrantes) DibujarEspaciosSobrantes(context, texto, y, fontSize);
            if (rainbowIndent) DibujarGuiaIndentacion(context, texto, y, nivelBrackets, fontSize);
            if (todoHighlight) DibujarTodoHighlight(context, texto, y, fontSize);

            // En la linea del cursor no se reemplazan por Pac-Man para poder editar la palabra real
            var comentarios = BuscarComentarios(texto, ref enComentarioBloque);
            var delimitadores = pacManBloques && i != Tab.FilaCursor ? BuscarDelimitadoresBloque(texto, comentarios) : null;
            bool ocultarDelimitadores = delimitadores is { Count: > 0 };

            if (texto.Length > 0)
            {
                var formateado = EditorFont.Formatear(texto, fontSize, colorTexto);
                foreach (var (inicio, fin) in comentarios) formateado.SetForegroundBrush(colorComentario, inicio, fin - inicio);
                if (ocultarDelimitadores)
                    foreach (var d in delimitadores!) formateado.SetForegroundBrush(Brushes.Transparent, d.Inicio, d.Largo);
                context.DrawText(formateado, new Point(MargenIzquierdo, y));
                DibujarSintaxis(context, texto, y, fontSize, comentarios, ocultarDelimitadores);
            }

            nivelBrackets = ProcesarBrackets(context, texto, y, nivelBrackets, fontSize, dibujar: bracketColorizer, ocultarPalabras: ocultarDelimitadores, comentarios: comentarios);

            if (ocultarDelimitadores) DibujarPacMans(context, texto, delimitadores!, y, fontSize);
        }

        DibujarCursor(context);
    }

    static readonly IBrush ComentarioClaro = new SolidColorBrush(Color.Parse("#008000"));
    public static readonly IBrush ComentarioOscuro = new SolidColorBrush(Color.Parse("#57A64A"));

    // Rangos [Inicio, Fin) de comentario: "/w" a fin de linea y "/all ... /end" multilinea (enBloque arrastra el estado)
    public static List<(int Inicio, int Fin)> BuscarComentarios(string texto, ref bool enBloque)
    {
        var rangos = new List<(int, int)>();
        int c = 0;
        if (enBloque)
        {
            int cierre = texto.IndexOf("/end", StringComparison.Ordinal);
            if (cierre < 0) { if (texto.Length > 0) rangos.Add((0, texto.Length)); return rangos; }
            rangos.Add((0, cierre + 4));
            c = cierre + 4;
            enBloque = false;
        }

        while (c < texto.Length)
        {
            char ch = texto[c];
            if (ch == '"')
            {
                int cierre = texto.IndexOf('"', c + 1);
                c = cierre < 0 ? texto.Length : cierre + 1;
                continue;
            }
            if (ch == '/' && c + 1 < texto.Length && texto[c + 1] == 'w')
            {
                rangos.Add((c, texto.Length));
                break;
            }
            if (string.CompareOrdinal(texto, c, "/all", 0, 4) == 0)
            {
                int cierre = texto.IndexOf("/end", c + 4, StringComparison.Ordinal);
                if (cierre < 0) { rangos.Add((c, texto.Length)); enBloque = true; break; }
                rangos.Add((c, cierre + 4));
                c = cierre + 4;
                continue;
            }
            c++;
        }
        return rangos;
    }

    static int FinDeComentarioEn(List<(int Inicio, int Fin)>? comentarios, int c)
    {
        if (comentarios == null) return -1;
        foreach (var (inicio, fin) in comentarios)
            if (c >= inicio && c < fin) return fin;
        return -1;
    }

    // Palabras clave con color de una linea, saltando strings y comentarios
    public static IEnumerable<(int Inicio, string Palabra, IBrush Color)> PalabrasColoreadas(string texto, List<(int Inicio, int Fin)> comentarios)
    {
        int c = 0;
        while (c < texto.Length)
        {
            char ch = texto[c];

            int finComentario = FinDeComentarioEn(comentarios, c);
            if (finComentario >= 0) { c = finComentario; continue; }

            if (ch == '"')
            {
                int cierre = texto.IndexOf('"', c + 1);
                c = cierre < 0 ? texto.Length : cierre + 1;
                continue;
            }

            if (!char.IsLetter(ch) && ch != '_') { c++; continue; }

            int inicio = c;
            while (c < texto.Length && (char.IsLetterOrDigit(texto[c]) || texto[c] == '_')) c++;

            string palabra = texto[inicio..c];
            if (ColoresSintaxis.TryGetValue(palabra, out var brush)) yield return (inicio, palabra, brush);
        }
    }

    // Repinta encima las palabras clave con color
    static void DibujarSintaxis(DrawingContext context, string texto, double y, double fontSize, List<(int Inicio, int Fin)> comentarios, bool ocultarDelimitadores = false)
    {
        foreach (var (inicio, palabra, brush) in PalabrasColoreadas(texto, comentarios))
        {
            if (ocultarDelimitadores && palabra is "glhf" or "gg") continue;
            double x = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..inicio], fontSize);
            context.DrawText(EditorFont.Formatear(palabra, fontSize, brush), new Point(x, y));
        }
    }

    static readonly IBrush AmarilloPacMan = new SolidColorBrush(Color.Parse("#FFE000"));
    const double AperturaBocaGrados = 35;
    const int SegmentosPacMan = 32;

    static List<(int Inicio, int Largo, bool Apertura)> BuscarDelimitadoresBloque(string texto, List<(int Inicio, int Fin)> comentarios)
    {
        var encontrados = new List<(int, int, bool)>();
        int c = 0;
        while (c < texto.Length)
        {
            char ch = texto[c];

            int finComentario = FinDeComentarioEn(comentarios, c);
            if (finComentario >= 0) { c = finComentario; continue; }

            if (ch == '"')
            {
                int cierre = texto.IndexOf('"', c + 1);
                c = cierre < 0 ? texto.Length : cierre + 1;
                continue;
            }

            if (!char.IsLetter(ch) && ch != '_') { c++; continue; }

            int inicio = c;
            while (c < texto.Length && (char.IsLetterOrDigit(texto[c]) || texto[c] == '_')) c++;

            string palabra = texto[inicio..c];
            if (palabra == "glhf") encontrados.Add((inicio, 4, true));
            else if (palabra == "gg") encontrados.Add((inicio, 2, false));
        }
        return encontrados;
    }

    // Cada Pac-Man ocupa el ancho de la palabra real, asi cursor y seleccion siguen midiendo el texto
    static void DibujarPacMans(DrawingContext context, string texto, List<(int Inicio, int Largo, bool Apertura)> delimitadores, double y, double fontSize)
    {
        double altoLinea = EditorFont.AltoLinea(fontSize);
        foreach (var (inicio, largo, apertura) in delimitadores)
        {
            double xInicio = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..inicio], fontSize);
            double xFin = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..(inicio + largo)], fontSize);
            double radio = Math.Min(fontSize * 0.42, (xFin - xInicio) / 2);
            var centro = new Point((xInicio + xFin) / 2, y + altoLinea / 2);

            DibujarPacMan(context, centro, radio, mirandoDerecha: apertura);
        }
    }

    // Poligono en vez de ArcTo: un SweepDirection invertido dibuja el gajo por fuera del circulo
    static void DibujarPacMan(DrawingContext context, Point centro, double radio, bool mirandoDerecha)
    {
        double direccion = mirandoDerecha ? 0 : 180;
        Point EnAngulo(double grados, double r)
        {
            double rad = grados * Math.PI / 180;
            return new Point(centro.X + r * Math.Cos(rad), centro.Y - r * Math.Sin(rad));
        }

        var cuerpo = new StreamGeometry();
        using (var g = cuerpo.Open())
        {
            g.BeginFigure(centro, isFilled: true);
            double barrido = 360 - 2 * AperturaBocaGrados;
            for (int s = 0; s <= SegmentosPacMan; s++)
                g.LineTo(EnAngulo(direccion + AperturaBocaGrados + barrido * s / SegmentosPacMan, radio));
            g.EndFigure(isClosed: true);
        }
        context.DrawGeometry(AmarilloPacMan, null, cuerpo);

        var ojo = new Point(centro.X + (mirandoDerecha ? 1 : -1) * radio * 0.15, centro.Y - radio * 0.5);
        context.DrawEllipse(Brushes.Black, null, ojo, radio * 0.13, radio * 0.13);
    }

    static void DibujarGuiaIndentacion(DrawingContext context, string texto, double y, int nivelBrackets, double fontSize)
    {
        int espacios = 0;
        while (espacios < texto.Length && texto[espacios] == ' ') espacios++;

        // El editor no auto-indenta, por eso tambien cuenta el nivel de brackets
        int niveles = Math.Max(espacios / 4, nivelBrackets);
        double altoLinea = EditorFont.AltoLinea(fontSize);
        double anchoNivel = EditorFont.MedirPrefijo("    ", fontSize);
        for (int nivel = 0; nivel < niveles; nivel++)
        {
            double x = MargenIzquierdo + nivel * anchoNivel;
            context.DrawLine(PlumasAnidado[nivel % PlumasAnidado.Length], new Point(x, y), new Point(x, y + altoLinea));
        }
    }

    static void DibujarEspaciosSobrantes(DrawingContext context, string texto, double y, double fontSize)
    {
        int inicio = texto.Length;
        while (inicio > 0 && texto[inicio - 1] == ' ') inicio--;
        if (inicio == texto.Length) return;

        double xInicio = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..inicio], fontSize);
        double xFin = MargenIzquierdo + EditorFont.MedirPrefijo(texto, fontSize);
        var rect = new Rect(xInicio, y, Math.Max(AnchoContinuacion, xFin - xInicio), EditorFont.AltoLinea(fontSize));
        context.FillRectangle(ColorEspaciosSobrantes, rect);
    }

    static void DibujarTodoHighlight(DrawingContext context, string texto, double y, double fontSize)
    {
        double altoLinea = EditorFont.AltoLinea(fontSize);
        foreach (var (palabra, color) in PalabrasTodo)
        {
            int idx = 0;
            while ((idx = texto.IndexOf(palabra, idx, StringComparison.Ordinal)) >= 0)
            {
                double xInicio = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..idx], fontSize);
                double xFin = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..(idx + palabra.Length)], fontSize);
                context.FillRectangle(color, new Rect(xInicio, y, Math.Max(AnchoContinuacion, xFin - xInicio), altoLinea));
                idx += palabra.Length;
            }
        }
    }

    // Cuenta el anidado siempre (rainbow-indent lo usa); solo dibuja si bracket-colorizer esta instalado. glhf/gg cuentan como par
    static int ProcesarBrackets(DrawingContext context, string texto, double y, int nivel, double fontSize, bool dibujar, bool ocultarPalabras = false, List<(int Inicio, int Fin)>? comentarios = null)
    {
        int c = 0;
        while (c < texto.Length)
        {
            int finComentario = FinDeComentarioEn(comentarios, c);
            if (finComentario >= 0) { c = finComentario; continue; }

            char ch = texto[c];
            bool esApertura = ch is '(' or '[' or '{';
            bool esCierre = ch is ')' or ']' or '}';
            int longitud = 1;

            if (!esApertura && !esCierre)
            {
                if (EsPalabraCompletaEn(texto, c, "glhf")) { esApertura = true; longitud = 4; }
                else if (EsPalabraCompletaEn(texto, c, "gg")) { esCierre = true; longitud = 2; }
            }

            if (esApertura || esCierre)
            {
                if (esCierre) nivel = Math.Max(0, nivel - 1);

                if (dibujar && !(ocultarPalabras && longitud > 1))
                {
                    double x = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..c], fontSize);
                    var brush = PaletaAnidado[nivel % PaletaAnidado.Length];
                    context.DrawText(EditorFont.Formatear(texto.Substring(c, longitud), fontSize, brush), new Point(x, y));
                }

                if (esApertura) nivel++;
            }

            c += longitud;
        }
        return nivel;
    }

    static bool EsPalabraCompletaEn(string texto, int posicion, string palabra)
    {
        if (posicion + palabra.Length > texto.Length) return false;
        if (string.CompareOrdinal(texto, posicion, palabra, 0, palabra.Length) != 0) return false;

        bool bordeIzquierdo = posicion == 0 || !char.IsLetterOrDigit(texto[posicion - 1]);
        int fin = posicion + palabra.Length;
        bool bordeDerecho = fin == texto.Length || !char.IsLetterOrDigit(texto[fin]);
        return bordeIzquierdo && bordeDerecho;
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

        double xInicio = EditorFont.MedirPrefijo(linea[..columnaInicio], TamanoFuente);
        double xFin = EditorFont.MedirPrefijo(linea[..columnaFin], TamanoFuente);
        if (indiceLinea != ff) xFin += AnchoContinuacion;

        var rect = new Rect(MargenIzquierdo + xInicio, y, Math.Max(AnchoContinuacion, xFin - xInicio), AltoLinea);
        context.FillRectangle(ColorSeleccion, rect);
    }

    public Rect ObtenerRectCursor()
    {
        if (Tab == null) return default;
        double x = MargenIzquierdo + EditorFont.MedirPrefijo(Tab.Lineas[Tab.FilaCursor][..Tab.ColumnaCursor], TamanoFuente);
        double y = 4 + Tab.FilaCursor * AltoLinea;
        return new Rect(x, y, 2, AltoLinea);
    }

    void DibujarCursor(DrawingContext context)
    {
        if (Tab == null || !IsFocused || !_caretVisible) return;

        double x = MargenIzquierdo + EditorFont.MedirPrefijo(Tab.Lineas[Tab.FilaCursor][..Tab.ColumnaCursor], TamanoFuente);
        double y = 4 + Tab.FilaCursor * AltoLinea;
        context.FillRectangle(ColorTexto, new Rect(x, y, 2, TamanoFuente));
    }

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

    // Ctrl+rueda = zoom; Handled corta la propagacion al ScrollViewer
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        ZoomSolicitado?.Invoke(e.Delta.Y > 0 ? 1 : -1);
        e.Handled = true;
    }

    (int fila, int columna) PosicionEnPixel(Point punto)
    {
        int fila = (int)((punto.Y - 4) / AltoLinea);
        fila = Math.Clamp(fila, 0, Tab!.Lineas.Count - 1);

        double xRelativo = punto.X - MargenIzquierdo;
        int columna = ColumnaEnX(Tab.Lineas[fila], xRelativo, TamanoFuente);
        return (fila, columna);
    }

    static int ColumnaEnX(string linea, double xRelativo, double fontSize)
    {
        double anchoPrevio = 0;
        for (int c = 0; c < linea.Length; c++)
        {
            // MedirPrefijo y no Medir: Medir da 0 en prefijos que terminan en blanco
            double anchoActual = EditorFont.MedirPrefijo(linea[..(c + 1)], fontSize);
            if (xRelativo < (anchoPrevio + anchoActual) / 2) return c;
            anchoPrevio = anchoActual;
        }
        return linea.Length;
    }

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

        // Key ignora shift: OemPlus cubre Ctrl+"=" y Ctrl+Shift+"="; Add/Subtract son del teclado numerico
        if (ctrl && e.Key is Key.OemPlus or Key.Add) { ZoomSolicitado?.Invoke(1); e.Handled = true; return; }
        if (ctrl && e.Key is Key.OemMinus or Key.Subtract) { ZoomSolicitado?.Invoke(-1); e.Handled = true; return; }
        if (ctrl && e.Key == Key.D0) { ZoomSolicitado?.Invoke(0); e.Handled = true; return; }

        if (alt && e.Key == Key.Up) { Tab.MoverLineaArriba(); e.Handled = true; return; }
        if (alt && e.Key == Key.Down) { Tab.MoverLineaAbajo(); e.Handled = true; return; }

        if (e.Key == Key.Enter) { if (!TryAutocompletarBloqueGlhf()) Tab.InsertarNuevaLinea(); e.Handled = true; return; }
        if (e.Key == Key.Tab) { Tab.InsertarTexto(new string(' ', TamanoTab)); e.Handled = true; return; }
        if (e.Key == Key.Back) { Tab.Retroceder(); e.Handled = true; return; }
        if (e.Key == Key.Delete) { Tab.Suprimir(); e.Handled = true; return; }

        if (e.Key == Key.Left) { Tab.MoverIzquierda(shift); e.Handled = true; return; }
        if (e.Key == Key.Right) { Tab.MoverDerecha(shift); e.Handled = true; return; }
        if (e.Key == Key.Up) { Tab.MoverArriba(shift); e.Handled = true; return; }
        if (e.Key == Key.Down) { Tab.MoverAbajo(shift); e.Handled = true; return; }
        if (e.Key == Key.Home) { Tab.MoverInicioLinea(shift); e.Handled = true; return; }
        if (e.Key == Key.End) { Tab.MoverFinLinea(shift); e.Handled = true; return; }
    }

    // Enter tras "glhf" arma el bloque con su "gg" de cierre (solo con Bracket Colorizer)
    bool TryAutocompletarBloqueGlhf()
    {
        if (Tab == null || Extensiones?.BracketColorizer.Instalada != true) return false;

        string linea = Tab.Lineas[Tab.FilaCursor];
        string antesDelCursor = linea[..Tab.ColumnaCursor];
        if (!antesDelCursor.EndsWith("glhf", StringComparison.Ordinal)) return false;

        int inicioPalabra = antesDelCursor.Length - 4;
        if (inicioPalabra > 0 && char.IsLetterOrDigit(antesDelCursor[inicioPalabra - 1])) return false;

        int espacios = 0;
        while (espacios < linea.Length && linea[espacios] == ' ') espacios++;
        string indentDentro = new string(' ', espacios + 4);
        string indentFuera = new string(' ', espacios);

        Tab.InsertarNuevaLinea();
        Tab.InsertarTexto(indentDentro);
        Tab.InsertarNuevaLinea();
        Tab.InsertarTexto(indentFuera + "gg");
        Tab.MoverCursor(Tab.FilaCursor - 1, indentDentro.Length, extenderSeleccion: false);
        return true;
    }

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
