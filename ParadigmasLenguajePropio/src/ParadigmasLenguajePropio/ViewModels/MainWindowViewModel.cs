using Avalonia.Input.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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
    public TerminalViewModel Terminal { get; } = new();
    public EditorAreaViewModel Editor { get; }
    public PanelJuegoViewModel PanelJuego { get; } = new();
    public ChatViewModel Chat { get; } = new();

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

    // px/seg: misma velocidad que velocidadColapsoExplorador/VelocidadAnimacion en el original
    const double VelocidadColapso = 1400;

    [ObservableProperty]
    private bool _exploradorVisible = true;

    // que se ve adentro del panel lateral izquierdo: el arbol de archivos o la lista de
    // extensiones (mismo lugar en pantalla, ver MainWindow.axaml). Elegir una de las dos fuerza
    // el panel a abrirse si estaba colapsado, igual que clickear un icono de la activity bar de VS Code.
    [ObservableProperty]
    private bool _mostrarExtensiones;

    [ObservableProperty]
    private double _anchoExplorador = AnchoExploradorInicial;

    [ObservableProperty]
    private bool _panelDerechoVisible = true;

    [ObservableProperty]
    private double _anchoPanelDerecho = AnchoPanelDerechoInicial;

    [ObservableProperty]
    private bool _terminalVisible;

    // panel de chat de la extension Claude, debajo de PanelJuego (ver MainWindow.axaml): una sola
    // instancia, se abre/cierra con el mismo boton de la activity bar que aparece al instalarla
    [ObservableProperty]
    private bool _chatVisible;

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
    double _anchoExploradorObjetivo = AnchoExploradorInicial;
    double _anchoPanelDerechoObjetivo = AnchoPanelDerechoInicial;
    DispatcherTimer? _timerColapso;
    DateTime _ultimoTickColapso;

    public MainWindowViewModel()
    {
        Explorer = new ExplorerViewModel(Dialogs);
        Editor = new EditorAreaViewModel(Dialogs);
        MenuBar.OnOpcionElegida = ManejarOpcionMenu;
        TitleBar.BuscarArchivos = Explorer.BuscarArchivosPorNombre;
        TitleBar.AbrirResultado = ruta => AbrirArchivo(Path.GetFileName(ruta), ruta);

        // el comando "exit" cierra el panel, igual que el toggle del menu Ver
        Terminal.OnCerrar = () => TerminalVisible = false;
        Chat.OnCerrar = () => ChatVisible = false;
        // la terminal se para en la carpeta abierta, como al abrir un proyecto en VSCode
        Explorer.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExplorerViewModel.CarpetaRaiz) && !string.IsNullOrEmpty(Explorer.CarpetaRaiz))
                Terminal.EstablecerDirectorio(Explorer.CarpetaRaiz);
        };

        Explorer.OnAbrirArchivo = nodo => AbrirArchivo(nodo.Nombre, nodo.RutaCompleta);
        Explorer.OnArchivoRenombrado = Editor.ActualizarRutaSiEstaAbierta;

        // la posicion del cursor en la barra de estado solo aparece con un archivo abierto
        Editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(EditorViewModel.LineaCursor) or nameof(EditorViewModel.ColumnaCursor) or nameof(EditorViewModel.HayArchivosAbiertos))
                ActualizarPosicionCursorEnEstado();
        };
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

    [RelayCommand]
    async Task ConstruirAsync()
    {
        await Editor.GuardarTodoAsync();
        TerminalVisible = true;
        Terminal.TabActiva = 1;
    }

    // comandos de la barra lateral de iconos (MainWindow.axaml): el original la tenia como una
    // columna vertical de botones a la izquierda, aparte del menu - la migracion la habia dejado
    // pendiente desde la Fase 2 y nunca se retomo
    [RelayCommand]
    void NuevoArchivo() => Editor.NuevoArchivo();

    [RelayCommand]
    Task Guardar() => Editor.GuardarAsync();

    [RelayCommand]
    void AlternarTerminal() => TerminalVisible = !TerminalVisible;

    [RelayCommand]
    void AlternarChat() => ChatVisible = !ChatVisible;

    [RelayCommand]
    void AlternarPausa() => PanelJuego.EnPausa = !PanelJuego.EnPausa;

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
            case "Terminal": TerminalVisible = !TerminalVisible; break;
            case "Panel derecho": AlternarPanelDerecho(); break;
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
            case "Ver salida": TerminalVisible = true; Terminal.TabActiva = 1; break;
            default: Console.WriteLine($"Menú: {opcion}"); break;
        }
    }

    [RelayCommand]
    void AlternarExplorador()
    {
        if (ExploradorVisible) _anchoExploradorGuardado = AnchoExplorador > 0 ? AnchoExplorador : _anchoExploradorGuardado;
        ExploradorVisible = !ExploradorVisible;
        _anchoExploradorObjetivo = ExploradorVisible ? _anchoExploradorGuardado : 0;
        AsegurarAnimacionColapsoCorriendo();
    }

    // botones dedicados de la activity bar (📁 / 🧩): a diferencia de AlternarExplorador, estos no
    // colapsan nada - solo eligen contenido y se aseguran de que el panel este abierto para verlo
    [RelayCommand]
    void MostrarExplorador()
    {
        MostrarExtensiones = false;
        if (!ExploradorVisible) AlternarExplorador();
    }

    [RelayCommand]
    void MostrarExtensionesPanel()
    {
        MostrarExtensiones = true;
        if (!ExploradorVisible) AlternarExplorador();
    }

    [RelayCommand]
    void AlternarPanelDerecho()
    {
        if (PanelDerechoVisible) _anchoPanelDerechoGuardado = AnchoPanelDerecho > 0 ? AnchoPanelDerecho : _anchoPanelDerechoGuardado;
        PanelDerechoVisible = !PanelDerechoVisible;
        _anchoPanelDerechoObjetivo = PanelDerechoVisible ? _anchoPanelDerechoGuardado : 0;
        AsegurarAnimacionColapsoCorriendo();
    }

    // reemplaza la easing manual con GetFrameTime() del original (para el explorador, en Ggez.cs,
    // y para el panel derecho, en PanelJuego.Update()): un unico timer que corre solo mientras algo
    // se esta animando, y se para solo cuando ambos anchos llegan a su objetivo.
    void AsegurarAnimacionColapsoCorriendo()
    {
        if (_timerColapso != null) return;
        _ultimoTickColapso = DateTime.UtcNow;
        _timerColapso = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        _timerColapso.Tick += (_, _) => TickAnimacionColapso();
        _timerColapso.Start();
    }

    void TickAnimacionColapso()
    {
        var ahora = DateTime.UtcNow;
        double paso = VelocidadColapso * (ahora - _ultimoTickColapso).TotalSeconds;
        _ultimoTickColapso = ahora;

        AnchoExplorador = Avanzar(AnchoExplorador, _anchoExploradorObjetivo, paso);
        AnchoPanelDerecho = Avanzar(AnchoPanelDerecho, _anchoPanelDerechoObjetivo, paso);

        if (AnchoExplorador == _anchoExploradorObjetivo && AnchoPanelDerecho == _anchoPanelDerechoObjetivo)
        {
            _timerColapso!.Stop();
            _timerColapso = null;
        }
    }

    static double Avanzar(double actual, double objetivo, double paso) =>
        Math.Abs(objetivo - actual) <= paso ? objetivo : actual + Math.Sign(objetivo - actual) * paso;
}
