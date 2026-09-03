using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de terminal: administra varias TerminalInstanceViewModel (hasta MaxTerminales) mostradas
// lado a lado en columnas, mas la pestaña fija "Errores" (salida de Construir, nunca se multiplica).
// Cada instancia es un proceso independiente, simulado o una PTY real segun la extension de Claude
// al momento de crearla (ver TerminalInstanceViewModel.EsReal) - ver esa clase para la logica de
// una sola terminal.
public partial class TerminalViewModel : ViewModelBase
{
    public const int MaxTerminales = 4;
    const int LimiteLineasErrores = 2000;

    public ObservableCollection<TerminalInstanceViewModel> Instancias { get; } = new();

    // subconjunto de Instancias con EnColumna=true, en el mismo orden - lo que arma la vista como
    // columnas (ver TerminalView.axaml.cs), reordenable arrastrando (ver ReordenarInstancia). Se
    // mantiene sincronizado a mano porque ItemsControl necesita una coleccion observable propia,
    // no un Where() recalculado cada vez.
    public ObservableCollection<TerminalInstanceViewModel> InstanciasVisibles { get; } = new();

    // el complemento: las que existen pero estan ocultas (ver CerrarDeVista) - la barra superior
    // las lista chiquitas para poder volver a mostrarlas (ver MostrarEnColumna)
    public ObservableCollection<TerminalInstanceViewModel> InstanciasOcultas { get; } = new();

    public bool PuedeCrearTerminal => Instancias.Count < MaxTerminales;

    // la unica instancia con foco de teclado a la vez, aunque todas las columnas se vean juntas -
    // se cambia clickeando la pestaña o el contenido de cualquier terminal (ver EnfocarInstancia)
    [ObservableProperty]
    private TerminalInstanceViewModel? _instanciaActiva;

    // la extension de Claude (ver ExtensionsViewModel.Claude): decide con que corre CADA columna
    // NUEVA (ver TerminalInstanceViewModel.EsReal) - una PTY real (ClaudeTerminalView) en vez de la
    // simulada por pipes, fijo para esa instancia de por vida (instalar/desinstalar la extension
    // despues no convierte retroactivamente las que ya estaban abiertas)
    public ExtensionInfo ClaudeExtension { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsTabTerminal), nameof(EsTabErrores))]
    private int _tabActiva;

    public bool EsTabTerminal => TabActiva == 0;
    public bool EsTabErrores => TabActiva == 1;

    // carpeta inicial de cada terminal nueva y de la terminal real (ver ClaudeTerminalView) - la
    // sigue el explorador de archivos (ver EstablecerDirectorio/RegistrarAperturaArchivo). Una vez
    // creada, cada instancia seguis su propio cwd (cd), esto solo semilla las nuevas.
    public string DirectorioActual => _directorioActual;
    string _directorioActual = Environment.CurrentDirectory;

    public ObservableCollection<TerminalLinea> LineasErrores { get; } = new();
    bool _huboErroresReales;

    public Action? OnCerrar; // lo dispara el comando "exit" de cualquier instancia

    public TerminalViewModel(ExtensionInfo claudeExtension)
    {
        ClaudeExtension = claudeExtension;

        LineasErrores.Add(new TerminalLinea("", "Los errores no existen", TerminalColorLinea.Escrito));

        // la primera terminal si se muestra de una (sin ella el panel arrancaria vacio); las que
        // se crean despues con "+" quedan en la lista de ocultas, el usuario decide donde ponerlas
        // arrastrandolas al area de columnas (ver CrearInstancia/TerminalView.axaml.cs)
        CrearInstancia(mostrarDeUna: true);
    }

    // el "+" del panel: la terminal nueva NO aparece sola como columna - queda en InstanciasOcultas
    // (se ve como un chip en la barra de arriba) hasta que el usuario la arrastra o la clickea
    [RelayCommand]
    void CrearTerminal() => CrearInstancia(mostrarDeUna: false);

    void CrearInstancia(bool mostrarDeUna)
    {
        if (!PuedeCrearTerminal) return;

        var instancia = new TerminalInstanceViewModel(_directorioActual, esReal: ClaudeExtension.Instalada)
        {
            Nombre = $"Terminal {Instancias.Count + 1}",
            EnColumna = mostrarDeUna,
        };
        instancia.OnCerrar = () => OnCerrar?.Invoke();
        instancia.OnError = AgregarError;
        instancia.OnActivar = () => EnfocarInstancia(instancia);
        instancia.PropertyChanged += Instancia_PropertyChanged;

        Instancias.Add(instancia);
        if (mostrarDeUna)
        {
            InstanciasVisibles.Add(instancia);
            TabActiva = 0;
            EnfocarInstancia(instancia);
        }
        else
        {
            InstanciasOcultas.Add(instancia);
        }

        OnPropertyChanged(nameof(PuedeCrearTerminal));
    }

    void Instancia_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TerminalInstanceViewModel.EnColumna) || sender is not TerminalInstanceViewModel instancia) return;

        if (instancia.EnColumna)
        {
            InstanciasOcultas.Remove(instancia);
            if (!InstanciasVisibles.Contains(instancia)) InstanciasVisibles.Add(instancia);
        }
        else
        {
            InstanciasVisibles.Remove(instancia);
            if (!InstanciasOcultas.Contains(instancia)) InstanciasOcultas.Add(instancia);
            // si se ocultaba la activa, el foco pasa a la primera columna que haya quedado visible
            if (InstanciaActiva == instancia) EnfocarInstancia(InstanciasVisibles.FirstOrDefault());
        }
    }

    // saca la columna de la vista pero la deja viva (proceso, historial, salida intactos) - se
    // puede volver a mostrar con MostrarEnColumna. Distinto de Eliminar, que la mata de verdad.
    [RelayCommand]
    void CerrarDeVista(TerminalInstanceViewModel instancia) => instancia.EnColumna = false;

    // click simple en un chip de la lista de ocultas: REEMPLAZA a la activa (la que estaba se va a
    // la lista, esta ocupa su mismo lugar) en vez de agregarse como columna nueva al lado - eso es
    // lo que hace arrastrarla (ver MostrarEnColumnaEnPosicion), un gesto mas explicito
    [RelayCommand]
    void MostrarEnColumna(TerminalInstanceViewModel instancia)
    {
        if (instancia.EnColumna) { TabActiva = 0; EnfocarInstancia(instancia); return; } // ya era visible, solo enfocar

        var anterior = InstanciaActiva;
        if (anterior != null && InstanciasVisibles.Contains(anterior))
        {
            int posicion = InstanciasVisibles.IndexOf(anterior);
            anterior.EnColumna = false;
            instancia.EnColumna = true;

            int posicionActual = InstanciasVisibles.IndexOf(instancia);
            int posicionFinal = Math.Clamp(posicion, 0, InstanciasVisibles.Count - 1);
            if (posicionActual != posicionFinal) InstanciasVisibles.Move(posicionActual, posicionFinal);
        }
        else
        {
            instancia.EnColumna = true;
        }

        TabActiva = 0;
        EnfocarInstancia(instancia);
    }

    // marca una instancia como la activa (click en su pestaña o en su contenido, ver
    // TerminalView.axaml.cs y TerminalInstanceView.axaml.cs) - null limpio quita el resaltado sin
    // marcar ninguna (ver Instancia_PropertyChanged al ocultar la ultima activa visible)
    [RelayCommand]
    void EnfocarInstancia(TerminalInstanceViewModel? instancia)
    {
        if (InstanciaActiva == instancia) return;
        if (InstanciaActiva != null) InstanciaActiva.EsActiva = false;
        InstanciaActiva = instancia;
        if (instancia != null) instancia.EsActiva = true;
    }

    // soltar una pestaña de terminal en el area de columnas (ver TerminalView.axaml.cs, arrastrada
    // desde otra columna para reordenar, o desde la lista de ocultas/pendientes para mostrarla por
    // primera vez) - "destino" es la posicion final entre las visibles, calculada a partir de la
    // posicion horizontal del mouse; solo al lado, nunca abajo de otra (no hay segunda dimension,
    // a diferencia del editor una lista plana ya alcanza)
    public void MostrarEnColumnaEnPosicion(TerminalInstanceViewModel instancia, int destino)
    {
        if (!InstanciasVisibles.Contains(instancia)) instancia.EnColumna = true; // la agrega al final via Instancia_PropertyChanged

        int origen = InstanciasVisibles.IndexOf(instancia);
        if (origen < 0) return; // no deberia pasar

        int nuevoIndice = Math.Clamp(destino > origen ? destino - 1 : destino, 0, InstanciasVisibles.Count - 1);
        if (nuevoIndice != origen) InstanciasVisibles.Move(origen, nuevoIndice);
        TabActiva = 0;
        EnfocarInstancia(instancia);
    }

    // el boton de basurero: a diferencia de CerrarDeVista, esto corta el proceso y la saca de
    // Instancias para siempre. Si era la ultima, se crea una nueva de una - el panel nunca se
    // queda sin ninguna terminal
    [RelayCommand]
    void Eliminar(TerminalInstanceViewModel instancia)
    {
        instancia.PropertyChanged -= Instancia_PropertyChanged;
        instancia.Cerrar();
        Instancias.Remove(instancia);
        InstanciasVisibles.Remove(instancia);
        InstanciasOcultas.Remove(instancia);
        OnPropertyChanged(nameof(PuedeCrearTerminal));

        if (InstanciaActiva == instancia) EnfocarInstancia(InstanciasVisibles.FirstOrDefault());
        if (Instancias.Count == 0) CrearInstancia(mostrarDeUna: true);
    }

    // ─────────────────────────── Errores ───────────────────────────

    // compartida entre todas las instancias (ver TerminalInstanceViewModel.OnError, conectado en
    // CrearTerminal): la salida de error de cualquier terminal, de cualquier comando, tambien
    // queda anotada aca para verla de un vistazo sin cambiar de instancia
    public void AgregarError(string texto)
    {
        if (!_huboErroresReales)
        {
            LineasErrores.Clear();
            _huboErroresReales = true;
        }

        LineasErrores.Add(new TerminalLinea("", texto, TerminalColorLinea.Error));
        while (LineasErrores.Count > LimiteLineasErrores) LineasErrores.RemoveAt(0);
    }

    // ─────────────────────────── directorio y limpieza ───────────────────────────

    public void RegistrarAperturaArchivo(string rutaCompleta)
    {
        string carpeta = Path.GetDirectoryName(rutaCompleta) ?? "";
        if (carpeta.Length > 0) EstablecerDirectorio(carpeta);
    }

    public void EstablecerDirectorio(string ruta)
    {
        if (!Directory.Exists(ruta)) return;
        _directorioActual = Path.GetFullPath(ruta);
    }

    // corta los procesos que hayan quedado vivos al cerrar la app, para no dejar huerfanos
    public void Cerrar()
    {
        foreach (var instancia in Instancias) instancia.Cerrar();
    }
}
