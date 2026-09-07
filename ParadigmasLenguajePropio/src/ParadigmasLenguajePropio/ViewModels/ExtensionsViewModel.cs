using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de extensiones: vive en el mismo lugar de pantalla que ExplorerView, se intercambian con
// el toggle de la barra de iconos (ver MainWindow.axaml / MainWindowViewModel.MostrarExtensiones).
// Lista <-> detalle es un solo estado (Seleccionada), no dos vistas separadas.
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

    // que Tema activa cada extension de tema y con que nombre corto se muestra en el selector
    // rapido (ver TemasInstalados) - agregar una fila aca alcanza para un tema nuevo (Contra)
    static readonly (string Id, Tema Tema, string Nombre)[] TemasDisponibles =
    [
        ("tema-zelda", Tema.Zelda, "Zelda"),
        ("tema-sonic", Tema.Sonic, "Sonic"),
        ("tema-megaman", Tema.MegaMan, "Mega Man"),
    ];

    static readonly IReadOnlyDictionary<string, Tema> TemasPorId =
        TemasDisponibles.ToDictionary(t => t.Id, t => t.Tema);

    // solo los temas con la extension instalada - Win95 no es una extension, el consumidor
    // (AppTitleBarViewModel.Temas via MainWindowViewModel) lo agrega aparte como opcion fija
    public IEnumerable<OpcionTema> TemasInstalados =>
        TemasDisponibles.Where(t => Extensiones.First(e => e.Id == t.Id).Instalada)
                         .Select(t => new OpcionTema(t.Nombre, t.Tema));

    // referencias directas por indice, mismo motivo que Claude: EditorTextArea (rainbow-indent,
    // trailing-whitespace, todo-highlight, bracket-colorizer) y MainWindow.axaml
    // (trailing-whitespace) necesitan consultar Instalada sin buscar por Id en cada frame
    public ExtensionInfo Claude => Extensiones[0];
    public ExtensionInfo RainbowIndent => Extensiones[1];
    public ExtensionInfo TrailingWhitespace => Extensiones[2];
    public ExtensionInfo TodoHighlight => Extensiones[3];
    public ExtensionInfo BracketColorizer => Extensiones[4];

    [ObservableProperty]
    private ExtensionInfo? _seleccionada;

    // recuerda entre sesiones que extensiones estaban instaladas (ver ExtensionesPersistenciaService):
    // se restaura al arrancar y se re-guarda cada vez que cualquiera cambia de estado, tanto por el
    // boton de Instalar/Desinstalar como por la animacion fantasma al terminar
    public ExtensionsViewModel()
    {
        var instaladas = ExtensionesPersistenciaService.CargarInstaladas();
        foreach (var ext in Extensiones)
        {
            ext.Instalada = instaladas.Contains(ext.Id);
            ext.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName != nameof(ExtensionInfo.Instalada)) return;
                GuardarInstaladas();
                if (!TemasPorId.ContainsKey(ext.Id)) return;
                AplicarTemaDeExtension(ext);
                OnPropertyChanged(nameof(TemasInstalados));
            };
        }

        // restaura el tema ACTIVO de la sesion anterior (distinto de instalado, ver
        // AplicarTemaDeExtension) - solo si su extension sigue instalada, por si el archivo de
        // configuracion quedo desincronizado a mano
        var temaGuardado = TemaService.CargarGuardado();
        bool sigueDisponible = temaGuardado == Tema.Win95 ||
            Extensiones.Any(e => TemasPorId.TryGetValue(e.Id, out var t) && t == temaGuardado && e.Instalada);
        TemaService.Aplicar(sigueDisponible ? temaGuardado : Tema.Win95);
    }

    void GuardarInstaladas() =>
        ExtensionesPersistenciaService.Guardar(Extensiones.Where(e => e.Instalada).Select(e => e.Id));

    // instalar un tema lo activa de una (como instalar un tema de VS Code); desinstalar solo
    // vuelve a Win95 si era el que estaba activo - instalado y activo son cosas distintas, igual
    // que cualquier otra extension: se puede tener varias instaladas y usar solo una a la vez
    void AplicarTemaDeExtension(ExtensionInfo ext)
    {
        var tema = TemasPorId[ext.Id];
        if (ext.Instalada) TemaService.Aplicar(tema);
        else if (TemaService.Actual == tema) TemaService.Aplicar(Tema.Win95);
    }

    // punto de entrada unico para elegir tema desde fuera del panel de extensiones (menu Ver, el
    // boton de pincel junto al buscador): Win95 solo activa, nunca desinstala nada; un tema no
    // instalado se instala (lo que ya lo activa via AplicarTemaDeExtension); uno ya instalado solo
    // se reactiva
    public void SeleccionarTema(Tema tema)
    {
        if (tema == Tema.Win95) { TemaService.Aplicar(Tema.Win95); return; }

        var ext = Extensiones.First(e => TemasPorId.TryGetValue(e.Id, out var t) && t == tema);
        if (ext.Instalada) TemaService.Aplicar(tema);
        else ext.Instalada = true;
    }

    [RelayCommand]
    void VerDetalle(ExtensionInfo ext) => Seleccionada = ext;

    [RelayCommand]
    void VolverALista() => Seleccionada = null;

    [RelayCommand]
    void AlternarInstalacion(ExtensionInfo ext)
    {
        if (ext.Instalada) { ext.Instalada = false; return; }
        InstalarConAnimacion(ext);
    }

    // "descarga fantasma": no hay red de por medio, el codigo de la extension ya esta compilado
    // en la app - esto solo simula una barra de progreso antes de prender Instalada
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
