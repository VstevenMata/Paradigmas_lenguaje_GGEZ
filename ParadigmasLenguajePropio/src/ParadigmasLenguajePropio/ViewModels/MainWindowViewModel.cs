using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

public partial class MainWindowViewModel : ViewModelBase
{
    public AppTitleBarViewModel TitleBar { get; } = new();
    public MenuBarViewModel MenuBar { get; } = new();
    public StatusBarViewModel StatusBar { get; } = new();
    public DialogService Dialogs { get; } = new();
    public ExplorerViewModel Explorer { get; }
    public ExtensionsViewModel Extensions { get; } = new();
    public TerminalViewModel Terminal { get; }
    public EditorAreaViewModel Editor { get; }
    public SettingsViewModel Settings { get; }
    public PanelJuegoViewModel PanelJuego { get; } = new();

    // se asigna despues de crear la Window, la necesitan Cortar/Copiar/Pegar del menu Editar
    public IClipboard? Clipboard { get; set; }

    const double AnchoExploradorMinimo = 100;
    const double AnchoExploradorMaximo = 400;
    const double AnchoExploradorInicial = 220;
    const double AnchoPanelDerechoMinimo = 220;
    const double AnchoPanelDerechoMaximo = 560;
    const double AnchoPanelDerechoInicial = 340;
    const double AlturaTerminalMinima = 80;
    const double AlturaTerminalInicial = 160;

    [ObservableProperty]
    private bool _exploradorVisible = true;

    // que se ve adentro del panel lateral izquierdo: el arbol de archivos o la lista de
    // extensiones (mismo lugar en pantalla, ver MainWindow.axaml). Elegir una de las dos fuerza
    // el panel a abrirse si estaba colapsado, igual que clickear un icono de la activity bar de VS Code.
    [ObservableProperty]
    private bool _mostrarExtensiones;

    // idem MostrarExtensiones, para el panel de Ajustes - mutuamente excluyentes
    [ObservableProperty]
    private bool _mostrarAjustes;

    // true cuando ninguno de los dos paneles alternativos esta activo: ese es el momento en que
    // el panel lateral muestra el explorador de archivos (ver MainWindow.axaml)
    public bool MostrarPanelPorDefecto => !MostrarExtensiones && !MostrarAjustes;
    partial void OnMostrarExtensionesChanged(bool value) => OnPropertyChanged(nameof(MostrarPanelPorDefecto));
    partial void OnMostrarAjustesChanged(bool value) => OnPropertyChanged(nameof(MostrarPanelPorDefecto));

    [ObservableProperty]
    private double _anchoExplorador = AnchoExploradorInicial;

    [ObservableProperty]
    private bool _panelDerechoVisible = true;

    [ObservableProperty]
    private double _anchoPanelDerecho = AnchoPanelDerechoInicial;

    [ObservableProperty]
    private bool _terminalVisible;

    [ObservableProperty]
    private double _alturaTerminal = AlturaTerminalInicial;

    // recalculada por MainWindow.axaml.cs cuando cambia el alto disponible (ver PART_GridContenido):
    // la terminal debe poder crecer hasta la mitad del espacio, no un tope fijo en pixeles que en
    // una ventana grande se queda corto y en una chica ni se nota
    [ObservableProperty]
    private double _alturaTerminalMaxima = 600;

    partial void OnAlturaTerminalChanged(double value) => OnPropertyChanged(nameof(AlturaTerminalEfectiva));
    partial void OnTerminalVisibleChanged(bool value) => OnPropertyChanged(nameof(AlturaTerminalEfectiva));

    // si la ventana se achica y el tope baja de la altura actual, la terminal se ajusta con el resto
    partial void OnAlturaTerminalMaximaChanged(double value)
    {
        if (AlturaTerminal > value) AlturaTerminal = Math.Max(AlturaTerminalMinima, value);
    }

    // la fila de la terminal en el Grid se bindea a esto en vez de a AlturaTerminal directo: al
    // ocultarla (TerminalVisible=false) la fila debe colapsar a 0, no quedar en su ultima altura -
    // pero el GridSplitter que la agranda arrastrando hacia arriba solo debe tocar AlturaTerminal
    public double AlturaTerminalEfectiva
    {
        get => TerminalVisible ? AlturaTerminal : 0;
        set { if (TerminalVisible) AlturaTerminal = Math.Clamp(value, AlturaTerminalMinima, AlturaTerminalMaxima); }
    }

    double _anchoExploradorGuardado = AnchoExploradorInicial;
    double _anchoPanelDerechoGuardado = AnchoPanelDerechoInicial;

    public MainWindowViewModel()
    {
        Explorer = new ExplorerViewModel(Dialogs);
        // la pestaña de chat de la terminal solo se habilita con la extension instalada (ver
        // TerminalViewModel.ClaudeExtension / TerminalView.axaml)
        Terminal = new TerminalViewModel(Extensions.Claude);
        Editor = new EditorAreaViewModel(Dialogs, Extensions);
        Settings = new SettingsViewModel(Editor, Extensions);
        MenuBar.OnOpcionElegida = ManejarOpcionMenu;
        TitleBar.BuscarArchivos = Explorer.BuscarArchivosPorNombre;
        TitleBar.AbrirResultado = ruta => AbrirArchivo(Path.GetFileName(ruta), ruta);
        TitleBar.AplicarTema = Extensions.SeleccionarTema;
        // el selector rapido de temas solo lista los que estan instalados (mas Win95, siempre fijo)
        SincronizarTemasDisponibles();
        Extensions.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExtensionsViewModel.TemasInstalados)) SincronizarTemasDisponibles();
        };

        // el comando "exit" cierra el panel, igual que el toggle del menu Ver
        Terminal.OnCerrar = () => TerminalVisible = false;
        // click en una fila del panel "Problemas": abre el archivo (o lo enfoca si ya esta abierto)
        // y mueve el cursor a la linea reportada, mismo mecanismo que "Ir a linea"
        Terminal.AbrirProblema = (ruta, linea) =>
        {
            AbrirArchivo(Path.GetFileName(ruta), ruta);
            Editor.IrALinea(linea);
        };
        // la terminal se para en la carpeta abierta, como al abrir un proyecto en VSCode
        Explorer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ExplorerViewModel.CarpetaRaiz)) return;
            if (!string.IsNullOrEmpty(Explorer.CarpetaRaiz)) Terminal.EstablecerDirectorio(Explorer.CarpetaRaiz);
            Editor.CarpetaRaiz = Explorer.CarpetaRaiz;
        };

        Explorer.OnAbrirArchivo = nodo => AbrirArchivo(nodo.Nombre, nodo.RutaCompleta);
        Explorer.OnArchivoRenombrado = Editor.ActualizarRutaSiEstaAbierta;
        Editor.OnAbrirArchivoExterno = AbrirArchivo;
        Editor.OnRevelarCarpeta = ruta => { Explorer.RevelarCarpeta(ruta); MostrarExplorador(); };

        // la posicion del cursor en la barra de estado solo aparece con un archivo abierto
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditorViewModel.LineaCursor) or nameof(EditorViewModel.ColumnaCursor) or nameof(EditorViewModel.HayArchivosAbiertos))
                ActualizarPosicionCursorEnEstado();
        };

        RestaurarSesion();
    }

    void SincronizarTemasDisponibles()
    {
        TitleBar.Temas.Clear();
        TitleBar.Temas.Add(new OpcionTema("Win95", Tema.Win95));
        foreach (var opcion in Extensions.TemasInstalados) TitleBar.Temas.Add(opcion);
    }

    // reabre carpeta, layout de splits y terminales de la ultima sesion (ver EditorAreaViewModel.
    // RestaurarLayout/TerminalViewModel.RestaurarCantidad), saltando lo que ya no exista en disco
    void RestaurarSesion()
    {
        var sesion = SesionPersistenciaService.Cargar();
        if (sesion == null) return;

        if (!string.IsNullOrEmpty(sesion.CarpetaRaiz) && Directory.Exists(sesion.CarpetaRaiz))
            Explorer.AbrirCarpeta(sesion.CarpetaRaiz);

        Editor.RestaurarLayout(sesion.Layout);
        // por si no habia CarpetaRaiz (archivos sueltos sin proyecto): la terminal igual arranca
        // en la carpeta del archivo activo en vez de Environment.CurrentDirectory
        if (Editor.TabActiva != null) Terminal.RegistrarAperturaArchivo(Editor.TabActiva.RutaCompleta);

        Terminal.RestaurarCantidad(sesion.CantidadTerminales, sesion.TerminalActivaIndice);
    }

    // llamado desde MainWindow.axaml.cs al cerrar la ventana
    public void GuardarSesion()
    {
        SesionPersistenciaService.Guardar(new SesionGuardada(
            Explorer.CarpetaRaiz,
            Editor.GuardarLayout(),
            Terminal.Instancias.Count,
            Terminal.InstanciaActiva != null ? Terminal.Instancias.IndexOf(Terminal.InstanciaActiva) : 0));
    }

    void AbrirArchivo(string nombre, string rutaCompleta)
    {
        Editor.AbrirArchivo(nombre, rutaCompleta);
        Terminal.RegistrarAperturaArchivo(rutaCompleta);
    }

    void ActualizarPosicionCursorEnEstado()
    {
        StatusBar.PosicionCursor = Editor.HayArchivosAbiertos ? $"Linea {Editor.LineaCursor}, Columna {Editor.ColumnaCursor}" : "";
    }

    async Task CopiarAsync()
    {
        if (Clipboard == null) return;
        string? texto = null;
        Editor.Copiar(t => texto = t);
        if (texto != null) await Clipboard.SetTextAsync(texto);
    }

    async Task CortarAsync()
    {
        if (Clipboard == null) return;
        string? texto = null;
        Editor.Cortar(t => texto = t);
        if (texto != null) await Clipboard.SetTextAsync(texto);
    }

    async Task PegarAsync()
    {
        if (Clipboard == null) return;
        var texto = await Clipboard.TryGetTextAsync();
        if (texto != null) Editor.Pegar(texto);
    }

    // una carpeta abierta ya alcanza (el Explorer.PropertyChanged de mas abajo para en CarpetaRaiz);
    // un archivo abierto sin carpeta tambien sirve (Terminal.RegistrarAperturaArchivo usa su carpeta) -
    // sin ninguna de las dos, la terminal arrancaria en Environment.CurrentDirectory (la carpeta del
    // propio ejecutable, no la del proyecto del usuario)
    bool HayLugarParaTerminal => Explorer.HayProyecto || Editor.HayArchivosAbiertos;

    // Ejecuta el .ggez abierto con el traductor (Traductor/ggezc.py). El contrato esta en
    // Traductor/README.md: stdout es la salida del programa, stderr lleva los errores como
    // archivo:linea:columna: etapa: mensaje, y el codigo de salida dice si fallo. Nada de eso
    // necesita codigo nuevo aca: la terminal ya separa stdout de stderr, ya copia stderr a la
    // pestaña Errores y ya marca en rojo el codigo distinto de cero.
    [RelayCommand]
    async Task ConstruirAsync()
    {
        if (!HayLugarParaTerminal) return;
        await Editor.GuardarTodoAsync();
        TerminalVisible = true;
        // antes saltaba a la pestaña Errores (1); ahora que el comando corre de verdad, lo que
        // el usuario quiere ver primero es la salida del programa, que vive en Terminal (0).
        // Los errores igual se copian solos a la pestaña Errores (ver OnError).
        Terminal.TabActiva = 0;

        var terminal = Terminal.InstanciaParaComando;
        if (terminal == null) return;
        Terminal.EnfocarInstancia(terminal);

        // el traductor lee del disco, no del buffer: sin ruta no hay nada que ejecutar
        string? archivo = Editor.TabActiva?.RutaCompleta;
        if (string.IsNullOrEmpty(archivo))
        {
            terminal.EscribirAviso("Guarde el archivo antes de construir.");
            return;
        }

        if (!archivo.EndsWith(".ggez", StringComparison.OrdinalIgnoreCase))
        {
            terminal.EscribirAviso($"'{Path.GetFileName(archivo)}' no es un archivo .ggez.");
            return;
        }

        string? traductor = RutaDelTraductor();
        if (traductor == null)
        {
            terminal.EscribirAviso("No se encontro Traductor/ggezc.py subiendo desde el ejecutable.");
            return;
        }

        terminal.EjecutarComando($"python \"{traductor}\" run \"{archivo}\"");
    }

    // Busca Traductor/ggezc.py subiendo desde la carpeta del ejecutable: con 'dotnet run' el
    // binario queda en bin/Debug/netX/, varios niveles debajo de la raiz del repo. Subir hasta
    // encontrarlo evita tener que configurar la ruta a mano en cada maquina.
    static string? RutaDelTraductor()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta != null; carpeta = carpeta.Parent)
        {
            string candidato = Path.Combine(carpeta.FullName, "Traductor", "ggezc.py");
            if (File.Exists(candidato)) return candidato;
        }
        return null;
    }

    // comandos de la barra lateral de iconos (MainWindow.axaml): el original la tenia como una
    // columna vertical de botones a la izquierda, aparte del menu - la migracion la habia dejado
    // pendiente desde la Fase 2 y nunca se retomo
    [RelayCommand]
    void NuevoArchivo() => Editor.NuevoArchivo();

    [RelayCommand]
    Task Guardar() => Editor.GuardarAsync();

    // abrir se bloquea hasta que haya una carpeta o un archivo abierto (ver HayLugarParaTerminal);
    // cerrar nunca se bloquea
    [RelayCommand]
    void AlternarTerminal()
    {
        if (TerminalVisible) { TerminalVisible = false; return; }
        if (!HayLugarParaTerminal) return;
        TerminalVisible = true;
    }

    // el boton de Claude en la activity bar (ver Extensions.Claude.Instalada en MainWindow.axaml)
    // abre la terminal directo en su pestaña de Claude, en vez de un panel aparte
    [RelayCommand]
    void MostrarChatClaude()
    {
        if (!Extensions.Claude.Instalada || !HayLugarParaTerminal) return;
        TerminalVisible = true;
        Terminal.TabActiva = 0; // la pestaña "Terminal" ya es la real con la extension instalada
    }

    [RelayCommand]
    void AlternarPausa() => PanelJuego.EnPausa = !PanelJuego.EnPausa;

    // boton dedicado de la extension Trim Trailing Whitespace en la activity bar (ver
    // Extensions.TrailingWhitespace.Instalada en MainWindow.axaml), mismo patron que
    // MostrarChatClaude: solo actua si la extension esta instalada y hay un archivo abierto
    [RelayCommand]
    void LimpiarEspaciosSobrantes()
    {
        if (!Extensions.TrailingWhitespace.Instalada || !Editor.HayArchivosAbiertos) return;
        Editor.LimpiarEspaciosSobrantes();
    }

    [RelayCommand]
    void MostrarBuscar()
    {
        Editor.BuscarVisible = true;
        Editor.MostrarReemplazo = false;
    }

    void ManejarOpcionMenu(string opcion)
    {
        switch (opcion)
        {
            case "Explorador": AlternarExplorador(); break;
            case "Terminal": AlternarTerminal(); break;
            case "Panel derecho": AlternarPanelDerecho(); break;
            case "Ajustes": MostrarAjustesPanel(); break;
            case "Acercar": Editor.CambiarZoom(1); break;
            case "Alejar": Editor.CambiarZoom(-1); break;
            case "Restablecer zoom": Editor.CambiarZoom(0); break;
            case "Tema: Win95": Extensions.SeleccionarTema(Tema.Win95); break;
            case "Tema: Zelda": Extensions.SeleccionarTema(Tema.Zelda); break;
            case "Tema: Sonic": Extensions.SeleccionarTema(Tema.Sonic); break;
            case "Tema: Mega Man": Extensions.SeleccionarTema(Tema.MegaMan); break;
            case "Abrir carpeta": Explorer.AbrirCarpetaDialogoCommand.Execute(null); break;
            case "Cerrar carpeta": Explorer.CerrarCarpetaCommand.Execute(null); break;
            case "Acerca de": _ = Dialogs.ShowModalAsync("Acerca de", "Aqui va la informacion del acerca de", "Cerrar"); break;
            case "Sintaxis de GGEZ": _ = Dialogs.ShowModalAsync("Sintaxis", "Aqui va la informacion sobre la sintaxis", "Cerrar"); break;
            case "Atajos de teclado": _ = Dialogs.ShowModalAsync("Atajos de teclado", "Aqui va la informacion sobre los atajos de teclado", "Cerrar"); break;
            case "Nuevo": Editor.NuevoArchivo(); break;
            case "Guardar": _ = Editor.GuardarAsync(); break;
            case "Guardar como": _ = Editor.GuardarAsync(forzarElegirRuta: true); break;
            case "Guardar todo": _ = Editor.GuardarTodoAsync(); break;
            case "Deshacer": Editor.Deshacer(); break;
            case "Rehacer": Editor.Rehacer(); break;
            case "Cortar": _ = CortarAsync(); break;
            case "Copiar": _ = CopiarAsync(); break;
            case "Pegar": _ = PegarAsync(); break;
            case "Buscar": Editor.BuscarVisible = true; Editor.MostrarReemplazo = false; break;
            case "Buscar y reemplazar": Editor.BuscarVisible = true; Editor.MostrarReemplazo = true; break;
            case "Ir a línea": Editor.IrALineaVisible = true; break;
            case "Construir": _ = ConstruirAsync(); break;
            case "Pausar / Reanudar": PanelJuego.EnPausa = !PanelJuego.EnPausa; break;
            case "Ver salida": if (HayLugarParaTerminal) { TerminalVisible = true; Terminal.TabActiva = 1; } break;
            default: Console.WriteLine($"Menú: {opcion}"); break;
        }
    }

    [RelayCommand]
    void AlternarExplorador()
    {
        if (ExploradorVisible)
        {
            _anchoExploradorGuardado = AnchoExplorador > 0 ? AnchoExplorador : _anchoExploradorGuardado;
            AnchoExplorador = 0;
        }
        else
        {
            AnchoExplorador = _anchoExploradorGuardado;
        }
        ExploradorVisible = !ExploradorVisible;
    }

    // botones dedicados de la activity bar (📁 / 🧩): a diferencia de AlternarExplorador, estos no
    // colapsan nada - solo eligen contenido y se aseguran de que el panel este abierto para verlo
    [RelayCommand]
    void MostrarExplorador()
    {
        MostrarExtensiones = false;
        MostrarAjustes = false;
        if (!ExploradorVisible) AlternarExplorador();
    }

    [RelayCommand]
    void MostrarExtensionesPanel()
    {
        MostrarExtensiones = true;
        MostrarAjustes = false;
        if (!ExploradorVisible) AlternarExplorador();
    }

    [RelayCommand]
    void MostrarAjustesPanel()
    {
        MostrarAjustes = true;
        MostrarExtensiones = false;
        if (!ExploradorVisible) AlternarExplorador();
    }

    [RelayCommand]
    void AlternarPanelDerecho()
    {
        if (PanelDerechoVisible)
        {
            _anchoPanelDerechoGuardado = AnchoPanelDerecho > 0 ? AnchoPanelDerecho : _anchoPanelDerechoGuardado;
            AnchoPanelDerecho = 0;
        }
        else
        {
            AnchoPanelDerecho = _anchoPanelDerechoGuardado;
        }
        PanelDerechoVisible = !PanelDerechoVisible;
    }
}
