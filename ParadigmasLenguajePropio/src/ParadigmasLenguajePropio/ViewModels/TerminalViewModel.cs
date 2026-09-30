using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Text.RegularExpressions;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de terminal: varias instancias en columnas mas la pestaña fija "Errores"
public partial class TerminalViewModel : ViewModelBase
{
    public const int MaxTerminales = 4;
    const int LimiteLineasErrores = 2000;

    public ObservableCollection<TerminalInstanceViewModel> Instancias { get; } = new();

    // Solo para contar posiciones; el ItemsSource es Instancias para que ocultar no destruya la vista (y la PTY)
    public IEnumerable<TerminalInstanceViewModel> InstanciasVisibles => Instancias.Where(i => i.EnColumna);

    // Convierte una posicion entre las visibles a la posicion absoluta en Instancias
    int PosicionAbsoluta(int posicionVisible)
    {
        int vistos = 0;
        for (int i = 0; i < Instancias.Count; i++)
        {
            if (!Instancias[i].EnColumna) continue;
            if (vistos == posicionVisible) return i;
            vistos++;
        }
        return Instancias.Count;
    }

    public ObservableCollection<TerminalInstanceViewModel> InstanciasOcultas { get; } = new();

    public bool PuedeCrearTerminal => Instancias.Count < MaxTerminales;

    public bool MuestraBotonCrear => EsTabTerminal && PuedeCrearTerminal;

    [ObservableProperty]
    private TerminalInstanceViewModel? _instanciaActiva;

    // Destino de comandos del boton Construir; se saltan las PTY reales porque tienen su propia sesion
    public TerminalInstanceViewModel? InstanciaParaComando =>
        InstanciaActiva is { EnColumna: true, EsReal: false } activa
            ? activa
            : InstanciasVisibles.FirstOrDefault(i => !i.EsReal);

    // Decide si cada columna NUEVA es PTY real o simulada; no cambia las ya abiertas
    public ExtensionInfo ClaudeExtension { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsTabTerminal), nameof(EsTabErrores), nameof(EsTabProblemas), nameof(MuestraBotonCrear))]
    private int _tabActiva;

    public bool EsTabTerminal => TabActiva == 0;
    public bool EsTabErrores => TabActiva == 1;
    public bool EsTabProblemas => TabActiva == 2;

    public Action<string, int>? AbrirProblema { get; set; }

    // Solo semilla el cwd de las terminales nuevas; cada instancia sigue su propio cd
    public string DirectorioActual => _directorioActual;
    string _directorioActual = Environment.CurrentDirectory;

    public ObservableCollection<TerminalLinea> LineasErrores { get; } = new();
    bool _huboErroresReales;

    // Formato del traductor: "archivo.ggez:12:5: error: mensaje"; lo que no matchea no genera fila
    static readonly Regex RegexProblema = new(@"^(?<archivo>[^:]+):(?<linea>\d+):(?<columna>\d+):\s*(?<severidad>error|warning)?:?\s*(?<mensaje>.+)$", RegexOptions.Compiled);

    public ObservableCollection<ProblemaItem> Problemas { get; } = new();
    public bool HayProblemas => Problemas.Count > 0;

    [RelayCommand]
    void AbrirProblemaEnEditor(ProblemaItem problema) => AbrirProblema?.Invoke(problema.Archivo, problema.Linea);

    public Action? OnCerrar;

    public TerminalViewModel(ExtensionInfo claudeExtension)
    {
        ClaudeExtension = claudeExtension;

        LineasErrores.Add(new TerminalLinea("", "Los errores no existen", TerminalColorLinea.Escrito));
        Problemas.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HayProblemas));

        // Solo la primera se muestra; las creadas con "+" quedan ocultas hasta arrastrarlas
        CrearInstancia(mostrarDeUna: true);
    }

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
            TabActiva = 0;
            EnfocarInstancia(instancia);
        }
        else
        {
            InstanciasOcultas.Add(instancia);
        }

        OnPropertyChanged(nameof(PuedeCrearTerminal));
        OnPropertyChanged(nameof(MuestraBotonCrear));
    }

    void Instancia_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(TerminalInstanceViewModel.EnColumna) || sender is not TerminalInstanceViewModel instancia) return;

        if (instancia.EnColumna)
        {
            InstanciasOcultas.Remove(instancia);
        }
        else
        {
            if (!InstanciasOcultas.Contains(instancia)) InstanciasOcultas.Add(instancia);
            if (InstanciaActiva == instancia) EnfocarInstancia(InstanciasVisibles.FirstOrDefault());
        }
    }

    // Oculta la columna sin matar el proceso (Eliminar si la mata)
    [RelayCommand]
    void CerrarDeVista(TerminalInstanceViewModel instancia) => instancia.EnColumna = false;

    // Click en un chip oculto reemplaza a la activa en su mismo lugar
    [RelayCommand]
    void MostrarEnColumna(TerminalInstanceViewModel instancia)
    {
        if (instancia.EnColumna) { TabActiva = 0; EnfocarInstancia(instancia); return; }

        var anterior = InstanciaActiva;
        if (anterior != null && anterior.EnColumna)
        {
            int posicionAnterior = Instancias.IndexOf(anterior);
            anterior.EnColumna = false;
            instancia.EnColumna = true;

            int posicionInstancia = Instancias.IndexOf(instancia);
            int posicionFinal = posicionInstancia < posicionAnterior ? posicionAnterior - 1 : posicionAnterior;
            if (posicionInstancia != posicionFinal) Instancias.Move(posicionInstancia, posicionFinal);
        }
        else
        {
            instancia.EnColumna = true;
        }

        TabActiva = 0;
        EnfocarInstancia(instancia);
    }

    [RelayCommand]
    public void EnfocarInstancia(TerminalInstanceViewModel? instancia)
    {
        if (InstanciaActiva == instancia) return;
        if (InstanciaActiva != null) InstanciaActiva.EsActiva = false;
        InstanciaActiva = instancia;
        if (instancia != null) instancia.EsActiva = true;
    }

    // "destino" es la posicion final entre las visibles, calculada con la x del mouse
    public void MostrarEnColumnaEnPosicion(TerminalInstanceViewModel instancia, int destino)
    {
        instancia.EnColumna = true;

        int origen = Instancias.IndexOf(instancia);
        if (origen < 0) return;

        int destinoAbsoluto = PosicionAbsoluta(destino);
        int nuevoIndice = Math.Clamp(destinoAbsoluto > origen ? destinoAbsoluto - 1 : destinoAbsoluto, 0, Instancias.Count - 1);
        if (nuevoIndice != origen) Instancias.Move(origen, nuevoIndice);
        TabActiva = 0;
        EnfocarInstancia(instancia);
    }

    // Mata el proceso; si era la ultima se crea otra para que el panel nunca quede vacio
    [RelayCommand]
    void Eliminar(TerminalInstanceViewModel instancia)
    {
        instancia.PropertyChanged -= Instancia_PropertyChanged;
        instancia.Cerrar();
        Instancias.Remove(instancia);
        InstanciasOcultas.Remove(instancia);
        OnPropertyChanged(nameof(PuedeCrearTerminal));
        OnPropertyChanged(nameof(MuestraBotonCrear));

        if (InstanciaActiva == instancia) EnfocarInstancia(InstanciasVisibles.FirstOrDefault());
        if (Instancias.Count == 0) CrearInstancia(mostrarDeUna: true);
    }

    public void AgregarError(string texto)
    {
        if (!_huboErroresReales)
        {
            LineasErrores.Clear();
            _huboErroresReales = true;
        }

        LineasErrores.Add(new TerminalLinea("", texto, TerminalColorLinea.Error));
        while (LineasErrores.Count > LimiteLineasErrores) LineasErrores.RemoveAt(0);

        var match = RegexProblema.Match(texto);
        if (!match.Success) return;

        var severidad = match.Groups["severidad"].Value == "warning" ? TerminalColorLinea.Aviso : TerminalColorLinea.Error;
        Problemas.Add(new ProblemaItem(
            match.Groups["archivo"].Value,
            int.Parse(match.Groups["linea"].Value),
            int.Parse(match.Groups["columna"].Value),
            match.Groups["mensaje"].Value,
            severidad));
    }

    // Solo recrea la cantidad de columnas; la primera ya existe desde el ctor
    public void RestaurarCantidad(int cantidad, int indiceActiva)
    {
        while (Instancias.Count < cantidad && PuedeCrearTerminal) CrearInstancia(mostrarDeUna: true);
        if (indiceActiva >= 0 && indiceActiva < Instancias.Count) EnfocarInstancia(Instancias[indiceActiva]);
    }

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

    public void Cerrar()
    {
        foreach (var instancia in Instancias) instancia.Cerrar();
    }
}
