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

    public IClipboard? Clipboard { get; set; }
    // La pone MainWindow: cerrar pasa por Closing, que guarda la sesion y corta la terminal
    public Action? Salir { get; set; }

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

    [ObservableProperty]
    private bool _mostrarExtensiones;

    [ObservableProperty]
    private bool _mostrarAjustes;

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

    // La recalcula MainWindow.axaml.cs: la terminal puede crecer hasta la mitad del alto disponible
    [ObservableProperty]
    private double _alturaTerminalMaxima = 600;

    partial void OnAlturaTerminalChanged(double value) => OnPropertyChanged(nameof(AlturaTerminalEfectiva));
    partial void OnTerminalVisibleChanged(bool value) => OnPropertyChanged(nameof(AlturaTerminalEfectiva));

    partial void OnAlturaTerminalMaximaChanged(double value)
    {
        if (AlturaTerminal > value) AlturaTerminal = Math.Max(AlturaTerminalMinima, value);
    }

    // La fila colapsa a 0 al ocultar la terminal sin perder AlturaTerminal (la que toca el GridSplitter)
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
        Terminal = new TerminalViewModel(Extensions.Claude);
        Editor = new EditorAreaViewModel(Dialogs, Extensions);
        Settings = new SettingsViewModel(Editor, Extensions);
        MenuBar.OnOpcionElegida = ManejarOpcionMenu;
        TitleBar.BuscarArchivos = Explorer.BuscarArchivosPorNombre;
        TitleBar.AbrirResultado = ruta => AbrirArchivo(Path.GetFileName(ruta), ruta);
        TitleBar.AplicarTema = Extensions.SeleccionarTema;
        SincronizarTemasDisponibles();
        Extensions.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExtensionsViewModel.TemasInstalados)) SincronizarTemasDisponibles();
        };

        Terminal.OnCerrar = () => TerminalVisible = false;
        Terminal.OnEvento = PanelJuego.RecibirEvento;
        Terminal.AbrirProblema = (ruta, linea) =>
        {
            AbrirArchivo(Path.GetFileName(ruta), ruta);
            Editor.IrALinea(linea);
        };
        Explorer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName != nameof(ExplorerViewModel.CarpetaRaiz)) return;
            if (!string.IsNullOrEmpty(Explorer.CarpetaRaiz)) Terminal.EstablecerCarpetaRaiz(Explorer.CarpetaRaiz);
            Editor.CarpetaRaiz = Explorer.CarpetaRaiz;
        };

        Explorer.OnAbrirArchivo = nodo => AbrirArchivo(nodo.Nombre, nodo.RutaCompleta);
        Explorer.OnArchivoRenombrado = Editor.ActualizarRutaSiEstaAbierta;
        Editor.OnAbrirArchivoExterno = AbrirArchivo;
        Editor.OnRevelarCarpeta = ruta => { Explorer.RevelarCarpeta(ruta); MostrarExplorador(); };

        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditorViewModel.LineaCursor) or nameof(EditorViewModel.ColumnaCursor) or nameof(EditorViewModel.HayArchivosAbiertos))
                ActualizarPosicionCursorEnEstado();
            if (e.PropertyName is nameof(EditorAreaViewModel.TabActiva) or nameof(EditorViewModel.HayArchivosAbiertos))
                PanelJuego.EnfocarArchivo(Editor.TabActiva?.RutaCompleta);
        };

        RestaurarSesion();
        PanelJuego.EnfocarArchivo(Editor.TabActiva?.RutaCompleta);
    }

    void SincronizarTemasDisponibles()
    {
        TitleBar.Temas.Clear();
        TitleBar.Temas.Add(new OpcionTema("Win95", Tema.Win95));
        foreach (var opcion in Extensions.TemasInstalados) TitleBar.Temas.Add(opcion);
    }

    // Reabre carpeta, splits y terminales de la ultima sesion, saltando lo que ya no exista en disco
    void RestaurarSesion()
    {
        var sesion = SesionPersistenciaService.Cargar();
        if (sesion == null) return;

        if (!string.IsNullOrEmpty(sesion.CarpetaRaiz) && Directory.Exists(sesion.CarpetaRaiz))
            Explorer.AbrirCarpeta(sesion.CarpetaRaiz);

        Editor.RestaurarLayout(sesion.Layout);
        // Sin carpeta raiz, la terminal arranca en la carpeta del archivo activo
        if (Editor.TabActiva != null) Terminal.RegistrarAperturaArchivo(Editor.TabActiva.RutaCompleta);

        Terminal.RestaurarCantidad(sesion.CantidadTerminales, sesion.TerminalActivaIndice);
    }

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

    // Sin carpeta ni archivo abierto la terminal arrancaria en la carpeta del ejecutable
    bool HayLugarParaTerminal => Explorer.HayProyecto || Editor.HayArchivosAbiertos;

    // Ejecutar (F5): compila y corre; Compilar (F6): solo revisa. Contrato en Traductor/README.md
    [RelayCommand]
    Task Construir(EditorTabViewModel? tab) => LanzarTraductorAsync(tab, soloCompilar: false);

    [RelayCommand]
    Task Compilar(EditorTabViewModel? tab) => LanzarTraductorAsync(tab, soloCompilar: true);

    // Con proyecto abierto corre su archivo principal; sin proyecto, la pestaña activa (tab = null: la del panel enfocado)
    async Task LanzarTraductorAsync(EditorTabViewModel? tab, bool soloCompilar)
    {
        if (!HayLugarParaTerminal) return;
        await Editor.GuardarTodoAsync();
        TerminalVisible = true;
        Terminal.TabActiva = 0;
        Terminal.LimpiarProblemas();

        var terminal = Terminal.InstanciaParaComando();
        if (terminal == null)
        {
            Terminal.AgregarError($"No hay terminal libre para ejecutar: cierre una (maximo {TerminalViewModel.MaxTerminales}).");
            Terminal.TabActiva = 1;
            return;
        }
        Terminal.EnfocarInstancia(terminal);

        // El traductor lee del disco, no del buffer
        string? archivo = (tab ?? Editor.TabActiva)?.RutaCompleta;
        // Un .ggez junto a un nivel.json es un nivel: F5 corre sus pruebas en vez de ejecutarlo
        bool esNivel = !string.IsNullOrEmpty(archivo) && File.Exists(Path.Combine(Path.GetDirectoryName(archivo)!, "nivel.json"));

        if (!esNivel && Explorer.HayProyecto)
        {
            archivo = Explorer.RutaPrincipal;
            if (archivo == null)
            {
                terminal.EscribirAviso("Error de proyecto: no hay archivo principal. Clic derecho sobre un .ggez del arbol > Marcar como principal.");
                return;
            }
            if (!File.Exists(archivo))
            {
                terminal.EscribirAviso($"Error de proyecto: el archivo principal '{Path.GetFileName(archivo)}' ya no existe. Marque otro como principal.");
                return;
            }
        }

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

        if (InstalacionService.FaltaDesplegar) terminal.EscribirAviso("Preparando el traductor (solo pasa la primera vez)...");
        string? ggezc = await InstalacionService.ComandoTraductorAsync();
        if (ggezc == null)
        {
            terminal.EscribirAviso("No se encontro Traductor/ggezc.py subiendo desde el ejecutable.");
            return;
        }

        if (soloCompilar)
        {
            terminal.EjecutarComando($"{ggezc} compile \"{archivo}\"");
            return;
        }
        string carpeta = Path.GetDirectoryName(archivo)!;
        string comando = esNivel ? "check" : "run";
        if (terminal.EjecutarComando($"{ggezc} {comando} \"{archivo}\" --eventos"))
            PanelJuego.ReiniciarMana(esNivel ? carpeta : null);
    }


    [RelayCommand]
    void NuevoArchivo() => Editor.NuevoArchivo();

    [RelayCommand]
    Task Guardar() => Editor.GuardarAsync();

    [RelayCommand]
    void AlternarTerminal()
    {
        if (TerminalVisible) { TerminalVisible = false; return; }
        if (!HayLugarParaTerminal) return;
        TerminalVisible = true;
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
            case "Abrir carpeta": Explorer.AbrirCarpetaDialogoCommand.Execute(null); break;
            case "Cerrar carpeta": Explorer.CerrarCarpetaCommand.Execute(null); break;
            case "Acerca de": _ = Dialogs.ShowAcercaDeAsync(); break;
            case "Sintaxis de GGEZ": _ = Dialogs.ShowAyudaAsync("Sintaxis de GGEZ", "Empieza por el diccionario: cada palabra de GGEZ equivale a un símbolo que ya conoces de Java", Ayuda.Sintaxis); break;
            case "Atajos de teclado": _ = Dialogs.ShowAyudaAsync("Atajos de teclado", "Si el control enfocado usa la misma tecla, gana el control", Ayuda.Atajos); break;
            case "Nuevo": Editor.NuevoArchivo(); break;
            case "Guardar": _ = Editor.GuardarAsync(); break;
            case "Guardar como": _ = Editor.GuardarAsync(forzarElegirRuta: true); break;
            case "Guardar todo": _ = Editor.GuardarTodoAsync(); break;
            case "Salir": Salir?.Invoke(); break;
            case "Deshacer": Editor.Deshacer(); break;
            case "Rehacer": Editor.Rehacer(); break;
            case "Cortar": _ = CortarAsync(); break;
            case "Copiar": _ = CopiarAsync(); break;
            case "Pegar": _ = PegarAsync(); break;
            case "Buscar": Editor.BuscarVisible = true; Editor.MostrarReemplazo = false; break;
            case "Buscar y reemplazar": Editor.BuscarVisible = true; Editor.MostrarReemplazo = true; break;
            case "Ir a línea": Editor.IrALineaVisible = true; break;
            case "Ejecutar": _ = LanzarTraductorAsync(null, soloCompilar: false); break;
            case "Compilar": _ = LanzarTraductorAsync(null, soloCompilar: true); break;
            case "Ver errores": if (HayLugarParaTerminal) { TerminalVisible = true; Terminal.TabActiva = 1; } break;
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
