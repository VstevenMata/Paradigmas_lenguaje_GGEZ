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

// Reemplaza la mitad "texto" de PanelCentral.cs: dibujo via DrawingContext/FormattedText en vez de
// Raylib inmediato, hit-testing pixel->columna con FormattedText en vez de medir substring a mano,
// e IClipboard async en vez de Raylib.GetClipboardText_/SetClipboardText. El modelo de datos
// (lineas/cursor/seleccion/undo) vive intacto en EditorTabViewModel - esta clase es solo la
// capa de render + entrada.
public class EditorTextArea : Control
{
    const int MargenIzquierdo = 4;

    // zoom (Ctrl+rueda / Ctrl +/-/0, ver OnPointerWheelChanged y OnKeyDown): reemplaza los antiguos
    // FontSize/AltoLinea constantes. AltoLinea se deriva de TamanoFuente via EditorFont.AltoLinea
    // (mismo calculo que usa EditorGutter) para que los numeros de linea nunca se desalineen del
    // texto al cambiar el zoom.
    public static readonly StyledProperty<double> TamanoFuenteProperty =
        AvaloniaProperty.Register<EditorTextArea, double>(nameof(TamanoFuente), 18.0);

    public double TamanoFuente
    {
        get => GetValue(TamanoFuenteProperty);
        set => SetValue(TamanoFuenteProperty, value);
    }

    public double AltoLinea => EditorFont.AltoLinea(TamanoFuente);

    // los sincroniza a mano EditorView.axaml.cs en cada ScrollChanged (mismo patron que
    // EditorMinimap.AltoTotal/AltoVisible/Desplazamiento): con esto, Render() sabe que rango de
    // lineas cae dentro del viewport y no tiene que dibujar el documento entero en cada frame -
    // antes CADA resize/scroll/keystroke volvia a mandar a shapear (FormattedText, lo caro de
    // Skia) TODAS las lineas del archivo, incluso las que ni se ven.
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

    // espacios que inserta la tecla Tab (ver OnKeyDown) - no afecta el render, no necesita Refrescar()
    public static readonly StyledProperty<int> TamanoTabProperty =
        AvaloniaProperty.Register<EditorTextArea, int>(nameof(TamanoTab), 4);

    public int TamanoTab
    {
        get => GetValue(TamanoTabProperty);
        set => SetValue(TamanoTabProperty, value);
    }

    static readonly IBrush ColorSeleccion = new SolidColorBrush(Color.FromArgb(110, 51, 153, 255));
    static readonly IBrush ColorLineaActual = new SolidColorBrush(Color.FromRgb(235, 235, 235));

    // extension Modo Oscuro: fondo "Ranked Night" de la paleta GGEZ y el gris de "variable local"
    // para el texto sin resaltado (el negro de siempre no se leeria sobre este fondo)
    static readonly IBrush FondoOscuro = new SolidColorBrush(Color.Parse("#0F1117"));
    static readonly IBrush TextoOscuro = new SolidColorBrush(Color.Parse("#D4D4D4"));
    static readonly IBrush LineaActualOscura = new SolidColorBrush(Color.Parse("#1C2030"));

    // ancho del resaltado de seleccion en una linea vacia (o al final de una linea que sigue en
    // la proxima): OJO, no se puede medir con EditorFont.Medir(" ", FontSize) - el ancho de
    // tinta de un espacio da 0 en este motor de texto (no cuenta el avance de blancos), lo que
    // dejaria el resaltado invisible de nuevo. Un valor fijo, mas grande que un caracter
    // angosto tipico, se ve como un bloque de verdad en vez de una raya de unos pocos px.
    const double AnchoContinuacion = 10;

    // paleta compartida de Rainbow Indent Guides y Bracket Pair Colorizer (ver
    // DibujarGuiaIndentacion/ProcesarBrackets) - un mismo nivel de anidado usa el mismo color en
    // las guias y en los brackets. Los colores no se toman de ExtensionInfo.IconoColor (esa es la
    // placa de un solo color del panel de extensiones): aca hace falta una secuencia larga, una
    // por nivel de anidado. Paleta estilo Super Mario Bros, hasta 15 niveles antes de repetir.
    static readonly IBrush[] PaletaAnidado =
    {
        new SolidColorBrush(Color.Parse("#D82800")), // Rojo Mario
        new SolidColorBrush(Color.Parse("#5C94FC")), // Azul cielo
        new SolidColorBrush(Color.Parse("#FCE000")), // Amarillo bloque
        new SolidColorBrush(Color.Parse("#00A800")), // Verde tuberia
        new SolidColorBrush(Color.Parse("#FCA044")), // Piel
        new SolidColorBrush(Color.Parse("#7C1EFC")), // Morado (power-up/estrella)
        new SolidColorBrush(Color.Parse("#00E8D8")), // Cian (agua/hielo)
        new SolidColorBrush(Color.Parse("#F878F8")), // Rosa (Peach)
        new SolidColorBrush(Color.Parse("#F87800")), // Naranja intenso (fuego/lava)
        new SolidColorBrush(Color.Parse("#005800")), // Verde oscuro (arbustos)
        new SolidColorBrush(Color.Parse("#0000BC")), // Azul marino (agua profunda)
        new SolidColorBrush(Color.Parse("#58F898")), // Verde Yoshi
        new SolidColorBrush(Color.Parse("#FCBC00")), // Dorado moneda
        new SolidColorBrush(Color.Parse("#940000")), // Vino/rojo oscuro (fuego apagado)
        new SolidColorBrush(Color.Parse("#005850")), // Verde azul petroleo (agua nocturna)
    };
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
    static readonly IBrush Nulo = new SolidColorBrush(Color.FromArgb(179, 0x6B, 0x6B, 0x6B)); // opacidad 70%
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
        //Estructura
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

        //Declaraciones
        ["stash"] = Declaraciones,     
        ["hardcoded"] = Declaraciones, 
        ["noloot"] = ColorTipoDato,    
        ["afk"] = Nulo,                

        //Tipos de datos
        ["score"] = ColorTipoDato,
        ["aim"] = ColorTipoDato, 
        ["lore"] = ColorTipoDato,
        ["win"] = ColorTipoDato, 
        ["pixel"] = ColorTipoDato, 
        ["squad"] = ColorTipoDato,
        ["stack"] = ColorTipoDato, 
        ["matchmaking"] = ColorTipoDato,
        ["inventory"] = ColorTipoDato, 

        //Booleanos
        ["hit"] = VerdeVida, 
        ["miss"] = RojoDano, 

        //Flujo de control
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

        //Funciones
        ["combo"] = Declaraciones,
        ["drops"] = Declaraciones, 
        ["reward"] = Declaraciones, 

        //7. Manejo de errores
        ["clutch"] = ManejoDeErrores,
        ["revive"] = ManejoDeErrores, 
        ["checkpoint"] = ManejoDeErrores, 
        ["wipe"] = RojoDano, 

        //8. POO
        ["guild"] = Declaraciones,
        ["questline"] = Declaraciones, 
        ["evolves"] = Declaraciones,
        ["spawn"] = Declaraciones,
        ["me"] = Instancia, 
        ["open"] = Modificadores,
        ["party"] = Modificadores, 
        ["solo"] = Modificadores, 

        //Entrada, salida y bibliotecas
        ["load"] = Entrada,
        ["rage"] = RojoDano,
        ["physics"] = EstructuraDelPrograma, 
        ["rng"] = EstructuraDelPrograma,
        ["clock"] = EstructuraDelPrograma, 
        ["save"] = EstructuraDelPrograma,

        //Incremento y decremento
        ["buff"] = Suma,
        ["nerf"] = Resta,

        //Delimitadores de bloque
        ["glhf"] = Delimitadores, 
        ["gg"] = Delimitadores,

        //Operadores aritmeticos
        ["gain"] = Suma, 
        ["drain"] = Resta, 
        ["crit"] = Critico, 
        ["split"] = Reparto,
        ["scrap"] = Reparto,

        //Operadores relacionales
        ["matches"] = Relacionales, 
        ["mismatch"] = Relacionales,
        ["over"] = Relacionales, 
        ["under"] = Relacionales, 
        ["atleast"] = Relacionales, 
        ["atmost"] = Relacionales, 

        //Asignacion
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

    // extensiones de resaltado (ver ExtensionsViewModel): rainbow-indent, trailing-whitespace,
    // todo-highlight y bracket-colorizer se consultan en cada Render() (ver mas abajo) - null
    // mientras la vista de diseño no tiene DataContext real
    public static readonly StyledProperty<ExtensionsViewModel?> ExtensionesProperty =
        AvaloniaProperty.Register<EditorTextArea, ExtensionsViewModel?>(nameof(Extensiones));

    public ExtensionsViewModel? Extensiones
    {
        get => GetValue(ExtensionesProperty);
        set => SetValue(ExtensionesProperty, value);
    }

    // se dispara cuando el cursor se mueve, para que la vista contenedora haga scroll si hace falta
    public event Action? CursorMovido;

    // se dispara con Ctrl+rueda o Ctrl +/-/0: la vista contenedora lo reenvia al ViewModel (que es
    // quien de verdad guarda TamanoFuente) en vez de que este control se cambie el zoom a si mismo -
    // mismo motivo que CursorMovido, mantener el estado en el ViewModel. 1 = acercar, -1 = alejar,
    // 0 = restablecer al tamaño por defecto.
    public event Action<int>? ZoomSolicitado;

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

        // el zoom cambia el ancho de cada linea (MeasureOverride) ademas de como se ve, por eso
        // Refrescar() (que invalida measure y visual) y no solo InvalidateVisual()
        TamanoFuenteProperty.Changed.AddClassHandler<EditorTextArea>((c, _) => c.Refrescar());

        // scroll: cambia que rango de lineas hay que dibujar, pero no el tamaño total del
        // documento - solo InvalidateVisual(), no hace falta volver a medir
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

        // sin esto, un Control sin Background no tiene nada "pintado" en las zonas donde no se
        // dibuja texto/resaltado (ej. una linea vacia que no es la actual) - y ahi el click
        // atraviesa el control (lo agarra el ScrollContentPresenter de atras) en vez de llegarle a
        // EditorTextArea. Mismo color que el fondo del Win95Bevel que lo envuelve (EditorView.axaml,
        // blanco u oscuro segun Modo Oscuro), asi que no cambia nada visualmente.
        bool modoOscuro = ModoOscuro;
        var colorTexto = ColorTexto;
        context.FillRectangle(modoOscuro ? FondoOscuro : Brushes.White, new Rect(Bounds.Size));

        bool rainbowIndent = Extensiones?.RainbowIndent.Instalada == true;
        bool espaciosSobrantes = Extensiones?.TrailingWhitespace.Instalada == true;
        bool todoHighlight = Extensiones?.TodoHighlight.Instalada == true;
        bool bracketColorizer = Extensiones?.BracketColorizer.Instalada == true;
        bool pacManBloques = Extensiones?.PacManBloques.Instalada == true;
        double fontSize = TamanoFuente; // cacheado: se lee una sola vez por frame, no por linea
        double altoLinea = AltoLinea;

        // viewport: que lineas caen dentro de lo que se ve (+2 de margen para que no se note un
        // parpadeo blanco al scrollear un pelin). Si todavia no llego el primer ScrollChanged
        // (AltoVisible en 0, ej. el primer frame) se dibuja el documento entero como antes -
        // mejor un frame de mas trabajo que un editor vacio.
        int primeraLinea = 0;
        int ultimaLinea = Tab.Lineas.Count - 1;
        if (AltoVisible > 0)
        {
            primeraLinea = Math.Max(0, (int)(DesplazamientoY / altoLinea) - 2);
            ultimaLinea = Math.Min(Tab.Lineas.Count - 1, (int)((DesplazamientoY + AltoVisible) / altoLinea) + 2);
        }

        // nivel de anidado de brackets: se acumula linea a linea (un bracket puede abrir en una
        // linea y cerrar varias mas abajo), por eso vive afuera del for en vez de reiniciarse por
        // linea. Se calcula SIEMPRE (no solo con bracket-colorizer instalado): rainbow-indent
        // tambien lo usa para saber el nivel de anidado de una linea sin indentacion real (este
        // editor no auto-indenta al escribir "{" + Enter, asi que contar solo espacios dejaba las
        // guias sin nada que dibujar en el uso tipico).
        // Para las lineas antes del viewport hace falta igual saber en que nivel se entra (si no,
        // el color de la guia/bracket "saltaria" al llegar ahi con scroll) - se cuenta sin dibujar,
        // que es mucho mas barato que shapear texto (no crea FormattedText, solo recorre chars).
        // mismo motivo para los comentarios de bloque "/all ... /end": pueden abrir varias lineas
        // arriba del viewport, hay que saber si la primera linea visible arranca adentro de uno
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

            // resaltados de fondo: van ANTES del texto para no taparlo. Los espacios sobrantes van
            // primero y las guias de indentacion despues: si una linea es solo espacios, las dos
            // se superponen - con las guias arriba siguen viendose sus colores en vez de quedar
            // lavadas bajo el rosa translucido del trim
            if (espaciosSobrantes) DibujarEspaciosSobrantes(context, texto, y, fontSize);
            if (rainbowIndent) DibujarGuiaIndentacion(context, texto, y, nivelBrackets, fontSize);
            if (todoHighlight) DibujarTodoHighlight(context, texto, y, fontSize);

            // extension Pac-Man Blocks: en la linea del cursor NO se reemplazan, para que se pueda
            // editar la palabra real (si no, borrar/mover dentro de un "glhf" seria a ciegas)
            var comentarios = BuscarComentarios(texto, ref enComentarioBloque);
            var delimitadores = pacManBloques && i != Tab.FilaCursor ? BuscarDelimitadoresBloque(texto, comentarios) : null;
            bool ocultarDelimitadores = delimitadores is { Count: > 0 };

            if (texto.Length > 0)
            {
                // comentarios en verde y palabras reemplazadas transparentes, sobre la misma linea
                // (no se tapan con un rectangulo): asi la seleccion, la linea actual y las guias de
                // indentacion siguen viendose atras
                var formateado = EditorFont.Formatear(texto, fontSize, colorTexto);
                foreach (var (inicio, fin) in comentarios) formateado.SetForegroundBrush(colorComentario, inicio, fin - inicio);
                if (ocultarDelimitadores)
                    foreach (var d in delimitadores!) formateado.SetForegroundBrush(Brushes.Transparent, d.Inicio, d.Largo);
                context.DrawText(formateado, new Point(MargenIzquierdo, y));
                DibujarSintaxis(context, texto, y, fontSize, comentarios, ocultarDelimitadores);
            }

            // recoloreado de brackets: va DESPUES del texto, se dibuja encima
            nivelBrackets = ProcesarBrackets(context, texto, y, nivelBrackets, fontSize, dibujar: bracketColorizer, ocultarPalabras: ocultarDelimitadores, comentarios: comentarios);

            if (ocultarDelimitadores) DibujarPacMans(context, texto, delimitadores!, y, fontSize);
        }

        DibujarCursor(context);
    }

    // ─────────────────────────── resaltado de sintaxis ───────────────────────────

    // verde de comentarios de Visual Studio para C++: el del tema oscuro no se lee sobre blanco,
    // por eso en modo claro va el verde del tema claro de VS
    static readonly IBrush ComentarioClaro = new SolidColorBrush(Color.Parse("#008000"));
    static readonly IBrush ComentarioOscuro = new SolidColorBrush(Color.Parse("#57A64A"));

    // rangos [Inicio, Fin) de la linea que son comentario, igual que el lexer del traductor:
    // "/w" hasta el fin de linea y "/all ... /end" (que puede abarcar varias lineas - por eso
    // enBloque entra con el estado de la linea anterior y sale con el de esta). Un "/w" dentro de
    // un string no cuenta.
    static List<(int Inicio, int Fin)> BuscarComentarios(string texto, ref bool enBloque)
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

    // si c cae dentro de un comentario devuelve donde termina ese comentario, si no -1
    static int FinDeComentarioEn(List<(int Inicio, int Fin)>? comentarios, int c)
    {
        if (comentarios == null) return -1;
        foreach (var (inicio, fin) in comentarios)
            if (c >= inicio && c < fin) return fin;
        return -1;
    }

    // misma tecnica que ProcesarBrackets: la linea ya se pinto en negro y aca se vuelven a
    // dibujar encima, en su misma x, las palabras que tienen color en ColoresSintaxis. Se saltan
    // los textos entre comillas y los comentarios (ver BuscarComentarios), para no pintar un
    // "score" que aparezca dentro de un string o comentario.
    static void DibujarSintaxis(DrawingContext context, string texto, double y, double fontSize, List<(int Inicio, int Fin)> comentarios, bool ocultarDelimitadores = false)
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
            if (ocultarDelimitadores && palabra is "glhf" or "gg") continue;

            if (ColoresSintaxis.TryGetValue(palabra, out var brush))
            {
                double x = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..inicio], fontSize);
                context.DrawText(EditorFont.Formatear(palabra, fontSize, brush), new Point(x, y));
            }
        }
    }

    // ─────────────────────────── extension: Pac-Man Blocks ───────────────────────────

    static readonly IBrush AmarilloPacMan = new SolidColorBrush(Color.Parse("#FFE000"));
    const double AperturaBocaGrados = 35; // mitad del angulo de la boca, a cada lado de hacia donde mira
    const int SegmentosPacMan = 32;

    // mismo recorrido que DibujarSintaxis (salta strings y comentarios), para no reemplazar un
    // "gg" que aparezca dentro de un texto entre comillas
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

    // cada Pac-Man ocupa exactamente el ancho de la palabra que reemplaza: asi el cursor, el click y
    // la seleccion siguen midiendo el texto real (MedirPrefijo/ColumnaEnX) sin ninguna conversion
    static void DibujarPacMans(DrawingContext context, string texto, List<(int Inicio, int Largo, bool Apertura)> delimitadores, double y, double fontSize)
    {
        double altoLinea = EditorFont.AltoLinea(fontSize);
        foreach (var (inicio, largo, apertura) in delimitadores)
        {
            double xInicio = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..inicio], fontSize);
            double xFin = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..(inicio + largo)], fontSize);
            double radio = Math.Min(fontSize * 0.42, (xFin - xInicio) / 2);
            var centro = new Point((xInicio + xFin) / 2, y + altoLinea / 2);

            // glhf abre el bloque mirando a la izquierda, gg lo cierra mirando a la derecha
            DibujarPacMan(context, centro, radio, mirandoDerecha: !apertura);
        }
    }

    // el cuerpo es un poligono de SegmentosPacMan lados en vez de un ArcTo: con un arco hay que
    // acertar SweepDirection/isLargeArc, y si se invierte sale un gajo por fuera del circulo
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

        // ojo: arriba del centro, un poco corrido hacia el lado de la boca
        var ojo = new Point(centro.X + (mirandoDerecha ? 1 : -1) * radio * 0.15, centro.Y - radio * 0.5);
        context.DrawEllipse(Brushes.Black, null, ojo, radio * 0.13, radio * 0.13);
    }

    // ─────────────────────────── extension: Rainbow Indent Guides ───────────────────────────

    static void DibujarGuiaIndentacion(DrawingContext context, string texto, double y, int nivelBrackets, double fontSize)
    {
        int espacios = 0;
        while (espacios < texto.Length && texto[espacios] == ' ') espacios++;

        // lo que sea mayor entre espacios reales (archivos con indentacion a mano, ej. GGEZ pegado
        // de otro lado) y nivel de brackets (uso tipico aca: el editor no auto-indenta al escribir
        // "{" + Enter, asi que contar solo espacios casi nunca tendria nada que mostrar)
        int niveles = Math.Max(espacios / 4, nivelBrackets);
        double altoLinea = EditorFont.AltoLinea(fontSize);
        for (int nivel = 0; nivel < niveles; nivel++)
        {
            double x = MargenIzquierdo + EditorFont.MedirPrefijo(new string(' ', nivel * 4), fontSize);
            var pluma = new Pen(PaletaAnidado[nivel % PaletaAnidado.Length], 1);
            context.DrawLine(pluma, new Point(x, y), new Point(x, y + altoLinea));
        }
    }

    // ─────────────────────────── extension: Trim Trailing Whitespace ───────────────────────────

    static void DibujarEspaciosSobrantes(DrawingContext context, string texto, double y, double fontSize)
    {
        int inicio = texto.Length;
        while (inicio > 0 && texto[inicio - 1] == ' ') inicio--;
        if (inicio == texto.Length) return; // sin espacios sobrantes

        double xInicio = MargenIzquierdo + EditorFont.MedirPrefijo(texto[..inicio], fontSize);
        double xFin = MargenIzquierdo + EditorFont.MedirPrefijo(texto, fontSize);
        var rect = new Rect(xInicio, y, Math.Max(AnchoContinuacion, xFin - xInicio), EditorFont.AltoLinea(fontSize));
        context.FillRectangle(ColorEspaciosSobrantes, rect);
    }

    // ─────────────────────────── extension: TODO Highlight ───────────────────────────

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

    // ─────────────────────────── extension: Bracket Pair Colorizer ───────────────────────────

    // cuenta el nivel de anidado SIEMPRE (lo necesita tambien rainbow-indent, ver Render); solo
    // dibuja los caracteres recoloreados si bracket-colorizer esta instalado.
    // Ademas de ( ) [ ] { }, reconoce "glhf"/"gg" como un par de bloque (ver
    // TryAutocompletarBloqueGlhf) - palabra completa, no una coincidencia adentro de otra palabra
    // (para no confundir un "gg" que aparece dentro de otra palabra).
    // ocultarPalabras: con Pac-Man Blocks activo en la linea, glhf/gg se cuentan igual (el nivel
    // tiene que seguir bien) pero no se repintan encima del Pac-Man.
    // comentarios: un bracket o un "gg" dentro de un comentario no abre ni cierra nada
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

    // Ctrl+rueda = zoom, en vez de dejar que el ScrollViewer que envuelve a este control scrollee
    // verticalmente. e.Handled=true corta la propagacion del evento hacia ese ScrollViewer (ruteo
    // bubble); sin Ctrl, cae al comportamiento normal de scroll sin tocar nada aca.
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
            // MedirPrefijo (no Medir): un prefijo que termina en blanco (ej. clickear dentro de la
            // indentacion, o en una linea que es solo espacios) mide 0 con Medir - el click
            // colapsaba siempre al final de la linea en vez de a la columna real
            double anchoActual = EditorFont.MedirPrefijo(linea[..(c + 1)], fontSize);
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

        // zoom: OemPlus/OemMinus son la tecla fisica "+/-" del teclado (Key reporta la tecla sin
        // aplicar shift, por eso Ctrl+"=" y Ctrl+Shift+"=" entran los dos por la misma rama); Add/
        // Subtract son el +/- del teclado numerico
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

    // extension Bracket Pair Colorizer: si la palabra justo antes del cursor es "glhf" (el par de
    // bloque de esta extension, ver ProcesarBrackets), Enter no solo abre una linea - arma el
    // bloque de una: linea vacia indentada un nivel mas para escribir adentro, y el "gg" de cierre
    // ya alineado con "glhf" debajo. Gateado por BracketColorizer.Instalada porque es esa
    // extension la que trata a glhf/gg como un par, igual que a { }
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
