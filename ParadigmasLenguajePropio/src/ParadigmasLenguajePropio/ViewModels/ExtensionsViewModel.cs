using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de extensiones; lista y detalle son un solo estado (Seleccionada)
public partial class ExtensionsViewModel : ViewModelBase
{
    public ObservableCollection<ExtensionInfo> Extensiones { get; } = new()
    {
        new ExtensionInfo("claude", "Claude para GGEZ", "Anthropic", "AI", "#D97757",
            "Asistente de IA integrado: chatea sobre el codigo abierto y aplica los cambios que sugiere.",
            new[] { "Panel de chat lateral", "Lee el archivo activo como contexto", "Aplica ediciones sugeridas al buffer" }),
        new ExtensionInfo("rainbow-indent", "Rainbow Indent Guides", "GGEZ Labs", "RB", "#7C3AED",
            "Colorea las guias de indentacion segun el nivel de anidamiento.",
            new[] { "Un color distinto por nivel de bloque", "Ayuda a ver donde cierra un for/if de un vistazo" }),
        new ExtensionInfo("trailing-whitespace", "Trim Trailing Whitespace", "GGEZ Labs", "WS", "#EF4444",
            "Resalta espacios sobrantes al final de linea y los limpia con un click.",
            new[] { "Resaltado de espacios invisibles", "Comando para limpiar todo el archivo de una" }),
        new ExtensionInfo("todo-highlight", "TODO Highlight", "GGEZ Labs", "TD", "#3B82F6",
            "Resalta comentarios TODO, FIXME y HACK para que no se pierdan en el archivo.",
            new[] { "Un color de fondo por tipo de comentario", "Se ven de un vistazo al bajar por el archivo" }),
        new ExtensionInfo("bracket-colorizer", "Bracket Pair Colorizer", "GGEZ Labs", "BR", "#10B981",
            "Colorea cada par de parentesis/llaves/corchetes segun su nivel de anidamiento.",
            new[] { "Emparejamiento visual de brackets", "Un color distinto por nivel de anidado" }),
        new ExtensionInfo("modo-oscuro", "Modo Oscuro", "GGEZ Labs", "DK", "#0F1117",
            "Pasa el fondo del editor a oscuro (Ranked Night) y aclara el texto para que se siga leyendo.",
            new[] { "Fondo oscuro en el editor y los numeros de linea", "El texto sin resaltado pasa de negro a gris claro" }),
        new ExtensionInfo("pacman-bloques", "Pac-Man Blocks", "GGEZ Labs", "PM", "#FFE000",
            "Muestra los delimitadores de bloque como Pac-Mans: glhf mira a la izquierda y gg a la derecha.",
            new[] { "Solo cambia como se ve, el archivo sigue guardando glhf/gg", "En la linea del cursor se ve el texto real para poder editarlo" }),
        new ExtensionInfo("ggez-formatter", "GGEZ Formatter", "GGEZ Labs", "FM", "#6B7280",
            "Reformatea codigo GGEZ (indentacion, espacios) reutilizando el lexer del traductor.",
            new[] { "Formatear el archivo completo", "Formatear al guardar (opcional)" }),
        new ExtensionInfo("tema-zelda", "Tema: Zelda", "GGEZ Labs", "ZE", "#1B5E20",
            "Reskin visual completo inspirado en las pantallas de menu de Zelda: paleta azul-indigo y dorada, tipografia pixel.",
            new[] { "Cambia colores, biseles y tipografia de toda la app", "Desinstalar vuelve al tema Win95 por defecto" }),
        new ExtensionInfo("tema-sonic", "Tema: Sonic", "GGEZ Labs", "SO", "#C1272D",
            "Reskin visual completo inspirado en Sonic the Hedgehog: paleta azul cobalto, rojo y dorado, tipografia pixel.",
            new[] { "Cambia colores, biseles y tipografia de toda la app", "Desinstalar vuelve al tema Win95 por defecto" }),
        new ExtensionInfo("tema-megaman", "Tema: Mega Man", "GGEZ Labs", "MM", "#2255CC",
            "Reskin visual completo inspirado en Mega Man: paleta azul cobalto y cian, contorno grueso tipo sprite de NES.",
            new[] { "Cambia colores, biseles y tipografia de toda la app", "Desinstalar vuelve al tema Win95 por defecto" }),
    };

    // Agregar una fila aca alcanza para un tema nuevo; Estrellas = las que pide para desbloquearse
    static readonly (string Id, Tema Tema, string Nombre, int Estrellas)[] TemasDisponibles =
    [
        ("tema-zelda", Tema.Zelda, "Zelda", 6),
        ("tema-sonic", Tema.Sonic, "Sonic", 25),
        ("tema-megaman", Tema.MegaMan, "Mega Man", 15),
    ];

    static readonly IReadOnlyDictionary<string, Tema> TemasPorId =
        TemasDisponibles.ToDictionary(t => t.Id, t => t.Tema);

    // Win95 no es extension: lo agrega aparte AppTitleBarViewModel
    public IEnumerable<OpcionTema> TemasInstalados =>
        TemasDisponibles.Where(t => Extensiones.First(e => e.Id == t.Id).Instalada)
                         .Select(t => new OpcionTema(t.Nombre, t.Tema));

    // Referencias por indice para consultar Instalada en cada frame sin buscar por Id
    public ExtensionInfo Claude => Extensiones[0];
    public ExtensionInfo RainbowIndent => Extensiones[1];
    public ExtensionInfo TrailingWhitespace => Extensiones[2];
    public ExtensionInfo TodoHighlight => Extensiones[3];
    public ExtensionInfo BracketColorizer => Extensiones[4];
    public ExtensionInfo ModoOscuro => Extensiones[5];
    public ExtensionInfo PacManBloques => Extensiones[6];

    [ObservableProperty]
    private ExtensionInfo? _seleccionada;

    public ExtensionsViewModel()
    {
        var instaladas = ExtensionesPersistenciaService.CargarInstaladas();
        foreach (var tema in TemasDisponibles)
            Extensiones.First(e => e.Id == tema.Id).EstrellasRequeridas = tema.Estrellas;

        foreach (var ext in Extensiones)
        {
            ext.Instalada = instaladas.Contains(ext.Id);
            ext.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ExtensionInfo.Instalada)) return;
                GuardarInstaladas();
                ActualizarBloqueos();
                if (!TemasPorId.ContainsKey(ext.Id)) return;
                AplicarTemaDeExtension(ext);
                OnPropertyChanged(nameof(TemasInstalados));
            };
        }

        // Restaura el tema activo solo si su extension sigue instalada
        var temaGuardado = TemaService.CargarGuardado();
        bool sigueDisponible = temaGuardado == Tema.Win95 ||
            Extensiones.Any(e => TemasPorId.TryGetValue(e.Id, out var t) && t == temaGuardado && e.Instalada);
        TemaService.Aplicar(sigueDisponible ? temaGuardado : Tema.Win95);

        ActualizarBloqueos();
        ProgresoService.Cambio += ActualizarBloqueos;
    }

    // Lo ya instalado no se quita aunque falten estrellas: solo se bloquea instalar
    void ActualizarBloqueos()
    {
        int estrellas = ProgresoService.EstrellasTotales;
        foreach (var ext in Extensiones) ext.Bloqueada = !ext.Instalada && ext.EstrellasRequeridas > estrellas;
    }

    void GuardarInstaladas() =>
        ExtensionesPersistenciaService.Guardar(Extensiones.Where(e => e.Instalada).Select(e => e.Id));

    // Instalar un tema lo activa; desinstalar vuelve a Win95 solo si era el activo
    void AplicarTemaDeExtension(ExtensionInfo ext)
    {
        var tema = TemasPorId[ext.Id];
        if (ext.Instalada) TemaService.Aplicar(tema);
        else if (TemaService.Actual == tema) TemaService.Aplicar(Tema.Win95);
    }

    public void SeleccionarTema(Tema tema)
    {
        if (tema == Tema.Win95) { TemaService.Aplicar(Tema.Win95); return; }

        var ext = Extensiones.First(e => TemasPorId.TryGetValue(e.Id, out var t) && t == tema);
        if (ext.Instalada) TemaService.Aplicar(tema);
        else if (!ext.Bloqueada) ext.Instalada = true;
    }

    [RelayCommand]
    void VerDetalle(ExtensionInfo ext) => Seleccionada = ext;

    [RelayCommand]
    void VolverALista() => Seleccionada = null;

    [RelayCommand]
    void AlternarInstalacion(ExtensionInfo ext)
    {
        if (ext.Instalada) { ext.Instalada = false; return; }
        if (ext.Bloqueada) return;
        InstalarConAnimacion(ext);
    }

    // Descarga simulada: el codigo ya esta compilado, solo anima el progreso
    static void InstalarConAnimacion(ExtensionInfo ext)
    {
        if (ext.Instalando) return;
        ext.Instalando = true;
        ext.Progreso = 0;
        var inicio = DateTime.UtcNow;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            double t = (DateTime.UtcNow - inicio).TotalSeconds / 0.9;
            ext.Progreso = Math.Min(1, t);
            if (t < 1) return;

            timer.Stop();
            ext.Instalando = false;
            ext.Instalada = true;
        };
        timer.Start();
    }
}
