using System.Collections.ObjectModel;
using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Reemplaza PanelCentral.cs (la parte de administracion de pestañas: abrir/cerrar/guardar/nuevo).
// El render/scroll/hit-testing del texto en sí vive en EditorTextArea/EditorGutter; la logica de
// edicion linea-a-linea vive en EditorTabViewModel.
public partial class EditorViewModel : ViewModelBase
{
    readonly DialogService _dialogos;

    // la necesita EditorTextArea (via EditorView.axaml) para saber que extensiones de resaltado
    // estan Instalada en cada frame de Render (rainbow-indent, trailing-whitespace,
    // todo-highlight, bracket-colorizer)
    public ExtensionsViewModel Extensiones { get; }

    // se asigna despues de crear la Window (necesita IStorageProvider para "Guardar como")
    public FileDialogService? Archivos { get; set; }

    public ObservableCollection<EditorTabViewModel> Tabs { get; } = new();

    [ObservableProperty]
    private EditorTabViewModel? _tabActiva;

    public bool HayArchivosAbiertos => Tabs.Count > 0;
    public string Texto => TabActiva?.Texto ?? "";
    public int LineaCursor => (TabActiva?.FilaCursor ?? 0) + 1;
    public int ColumnaCursor => (TabActiva?.ColumnaCursor ?? 0) + 1;

    [ObservableProperty] private string _carpetaRaiz = "";
    partial void OnCarpetaRaizChanged(string value) => ActualizarBreadcrumb();

    public Action<string, string>? OnAbrirArchivoExterno { get; set; }
    public Action<string>? OnRevelarCarpeta { get; set; }

    // "carpeta > subcarpeta > archivo.ggez", con cada segmento clickeable
    public IReadOnlyList<BreadcrumbSegmentoViewModel> BreadcrumbSegmentos { get; private set; } = Array.Empty<BreadcrumbSegmentoViewModel>();

    void ActualizarBreadcrumb()
    {
        BreadcrumbSegmentos = ConstruirBreadcrumb();
        OnPropertyChanged(nameof(BreadcrumbSegmentos));
    }

    List<BreadcrumbSegmentoViewModel> ConstruirBreadcrumb()
    {
        var segmentos = new List<BreadcrumbSegmentoViewModel>();
        if (TabActiva == null) return segmentos;

        void Agregar(string texto, string carpeta) =>
            segmentos.Add(new BreadcrumbSegmentoViewModel(texto, carpeta, mostrarSeparadorAntes: segmentos.Count > 0)
            {
                AlElegirArchivo = nodo => OnAbrirArchivoExterno?.Invoke(nodo.Nombre, nodo.RutaCompleta),
                AlElegirCarpeta = nodo => OnRevelarCarpeta?.Invoke(nodo.RutaCompleta),
            });

        // archivo sin guardar: sin ruta, no hay carpeta que listar
        if (string.IsNullOrEmpty(TabActiva.RutaCompleta))
        {
            Agregar(TabActiva.Nombre, "");
            return segmentos;
        }

        if (string.IsNullOrEmpty(CarpetaRaiz))
        {
            Agregar(TabActiva.RutaCompleta, Path.GetDirectoryName(TabActiva.RutaCompleta) ?? "");
            return segmentos;
        }

        var raiz = CarpetaRaiz.TrimEnd(Path.DirectorySeparatorChar);
        Agregar(Path.GetFileName(raiz), raiz);

        var partes = Path.GetRelativePath(CarpetaRaiz, TabActiva.RutaCompleta).Split(Path.DirectorySeparatorChar);
        string acumulada = raiz;
        for (int i = 0; i < partes.Length; i++)
        {
            bool esArchivo = i == partes.Length - 1;
            // el segmento del archivo lista la carpeta que lo contiene, no a si mismo
            if (!esArchivo) acumulada = Path.Combine(acumulada, partes[i]);
            Agregar(partes[i], acumulada);
        }
        return segmentos;
    }

    public void AlternarSegmentoBreadcrumb(BreadcrumbSegmentoViewModel segmento)
    {
        if (string.IsNullOrEmpty(segmento.CarpetaCompleta)) return;

        bool yaAbierto = segmento.EsAbierto;
        foreach (var s in BreadcrumbSegmentos) s.EsAbierto = false;
        segmento.EsAbierto = !yaAbierto;
    }

    // barra de buscar/reemplazar (BarraBusqueda.cs original) y el cuadro de ir a línea (CuadroIrALinea.cs)
    [ObservableProperty] private bool _buscarVisible;
    [ObservableProperty] private bool _mostrarReemplazo;
    [ObservableProperty] private string _patronBusqueda = "";
    [ObservableProperty] private string _textoReemplazo = "";
    [ObservableProperty] private bool _distingueMayusculas;
    [ObservableProperty] private string _contadorBusqueda = "";
    [ObservableProperty] private bool _irALineaVisible;
    [ObservableProperty] private string _numeroLineaTexto = "";

    // zoom del editor (Ctrl+rueda / Ctrl +/-/0 en EditorTextArea, o el menu Ver): independiente
    // por panel, igual que BuscarVisible/IrALineaVisible - cada split de EditorAreaViewModel tiene
    // su propio EditorViewModel y por lo tanto su propio zoom.
    const double ZoomMinimo = 10;
    const double ZoomMaximo = 36;
    const double ZoomPorDefecto = 18;
    const double ZoomPaso = 2;

    const int TamanoTabMinimo = 1;
    const int TamanoTabMaximo = 8;

    // ultimo tamaño/tab usado en cualquier panel, persistido en disco - los paneles nuevos arrancan con esto
    static readonly ConfiguracionUsuario _configuracionInicial = ConfiguracionService.CargarGuardada();
    public static double TamanoFuentePreferido { get; set; } = _configuracionInicial.TamanoFuente;
    public static int TamanoTabPreferido { get; set; } = _configuracionInicial.TamanoTab;

    [ObservableProperty] private double _tamanoFuente = TamanoFuentePreferido;
    [ObservableProperty] private int _tamanoTab = TamanoTabPreferido;

    partial void OnTamanoFuenteChanged(double value)
    {
        TamanoFuentePreferido = value;
        ConfiguracionService.Guardar(new ConfiguracionUsuario(value, TamanoTabPreferido));
    }

    partial void OnTamanoTabChanged(int value)
    {
        TamanoTabPreferido = value;
        ConfiguracionService.Guardar(new ConfiguracionUsuario(TamanoFuentePreferido, value));
    }

    // direccion: 1 acercar, -1 alejar, 0 restablecer al tamaño por defecto
    public void CambiarZoom(int direccion) => TamanoFuente = direccion switch
    {
        0 => ZoomPorDefecto,
        > 0 => Math.Min(ZoomMaximo, TamanoFuente + ZoomPaso),
        _ => Math.Max(ZoomMinimo, TamanoFuente - ZoomPaso),
    };

    public void CambiarTamanoTab(int direccion) =>
        TamanoTab = Math.Clamp(TamanoTab + direccion, TamanoTabMinimo, TamanoTabMaximo);

    // busca mientras se escribe, sin esperar Enter (como el original) - estos partial methods los
    // dispara el source generator de [ObservableProperty] solo, no hace falta suscribirse a mano
    partial void OnPatronBusquedaChanged(string value) => ActualizarBusqueda();
    partial void OnDistingueMayusculasChanged(bool value) => ActualizarBusqueda();

    public void ActualizarBusqueda()
    {
        var (actual, total) = TabActiva?.Buscar(PatronBusqueda, DistingueMayusculas, siguiente: true) ?? (0, 0);
        ContadorBusqueda = total == 0 ? "0/0" : $"{actual}/{total}";
    }

    public void BuscarSiguiente()
    {
        var (actual, total) = TabActiva?.Buscar(PatronBusqueda, DistingueMayusculas, siguiente: true) ?? (0, 0);
        ContadorBusqueda = total == 0 ? "0/0" : $"{actual}/{total}";
    }

    public void BuscarAnterior()
    {
        var (actual, total) = TabActiva?.Buscar(PatronBusqueda, DistingueMayusculas, siguiente: false) ?? (0, 0);
        ContadorBusqueda = total == 0 ? "0/0" : $"{actual}/{total}";
    }

    public void ReemplazarUno()
    {
        var (actual, total) = TabActiva?.Reemplazar(PatronBusqueda, TextoReemplazo, DistingueMayusculas) ?? (0, 0);
        ContadorBusqueda = total == 0 ? "0/0" : $"{actual}/{total}";
    }

    public void ReemplazarTodo() => TabActiva?.ReemplazarTodo(PatronBusqueda, TextoReemplazo, DistingueMayusculas);

    public void ConfirmarIrALinea()
    {
        if (int.TryParse(NumeroLineaTexto, out int numero)) IrALinea(numero);
        IrALineaVisible = false;
        NumeroLineaTexto = "";
    }

    public EditorViewModel(DialogService dialogos, ExtensionsViewModel extensiones)
    {
        _dialogos = dialogos;
        Extensiones = extensiones;
        Tabs.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HayArchivosAbiertos));
    }

    // crea una pestaña nueva sin ruta: recién se asigna un archivo real al guardarla
    public void NuevoArchivo()
    {
        var tab = new EditorTabViewModel(NombreNuevoDisponible(), "");
        Tabs.Add(tab);
        CambiarArchivoActivo(tab);
    }

    string NombreNuevoDisponible()
    {
        const string baseNombre = "Sin título";
        if (Tabs.All(a => a.Nombre != baseNombre)) return baseNombre;

        int n = 2;
        while (Tabs.Any(a => a.Nombre == $"{baseNombre} {n}")) n++;
        return $"{baseNombre} {n}";
    }

    // evita abrir el mismo archivo dos veces en pestañas distintas
    public void AbrirArchivo(string nombre, string rutaCompleta)
    {
        var existente = Tabs.FirstOrDefault(a => a.RutaCompleta == rutaCompleta);
        if (existente != null)
        {
            if (existente != TabActiva) CambiarArchivoActivo(existente);
            return;
        }

        List<string> contenido;
        try
        {
            string texto = File.ReadAllText(rutaCompleta);
            contenido = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            if (contenido.Count == 0) contenido.Add("");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo abrir {rutaCompleta}: {ex.Message}");
            return;
        }

        var tab = new EditorTabViewModel(nombre, rutaCompleta, contenido);
        Tabs.Add(tab);
        CambiarArchivoActivo(tab);
    }

    public void CambiarArchivoActivo(EditorTabViewModel tab)
    {
        if (TabActiva != null) { TabActiva.EsActiva = false; TabActiva.CursorCambiado -= AvisarCursor; TabActiva.PropertyChanged -= TabActiva_PropertyChanged; }
        TabActiva = tab;
        tab.EsActiva = true;
        tab.CursorCambiado += AvisarCursor;
        tab.PropertyChanged += TabActiva_PropertyChanged;
        AvisarCursor();
        ActualizarBreadcrumb();
    }

    void AvisarCursor()
    {
        OnPropertyChanged(nameof(LineaCursor));
        OnPropertyChanged(nameof(ColumnaCursor));
    }

    void TabActiva_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(EditorTabViewModel.RutaCompleta) or nameof(EditorTabViewModel.Nombre))
            ActualizarBreadcrumb();
    }

    public async Task CerrarArchivoAsync(EditorTabViewModel tab)
    {
        if (tab.Modificado)
        {
            var eleccion = await _dialogos.ShowModalAsync("Confirmar", $"Cerrar \"{tab.Nombre}\" sin guardar los cambios?", "Si", "No");
            if (eleccion != 0) return;
        }
        QuitarTab(tab);
    }

    // saca la pestaña de este panel y reacomoda TabActiva - sin confirmar nada, a diferencia
    // de CerrarArchivoAsync: la usa tambien EditorAreaViewModel al arrastrar una pestaña a
    // otro panel, donde "cerrar sin guardar" no aplica porque el archivo no se pierde
    public void QuitarTab(EditorTabViewModel tab)
    {
        int indice = Tabs.IndexOf(tab);
        bool eraActiva = tab == TabActiva;
        Tabs.RemoveAt(indice);

        if (!eraActiva) return;

        if (Tabs.Count == 0)
        {
            if (TabActiva != null) { TabActiva.CursorCambiado -= AvisarCursor; TabActiva.PropertyChanged -= TabActiva_PropertyChanged; }
            TabActiva = null;
            AvisarCursor();
            ActualizarBreadcrumb();
        }
        else
        {
            CambiarArchivoActivo(Tabs[Math.Min(indice, Tabs.Count - 1)]);
        }
    }

    // si ese archivo está abierto en una pestaña, la sincroniza con su nueva ruta (ej: se renombró desde el explorador)
    public void ActualizarRutaSiEstaAbierta(string rutaVieja, string rutaNueva, string nombreNuevo)
    {
        var tab = Tabs.FirstOrDefault(a => a.RutaCompleta == rutaVieja);
        if (tab == null) return;
        tab.RutaCompleta = rutaNueva;
        tab.Nombre = nombreNuevo;
    }

    public async Task GuardarAsync(bool forzarElegirRuta = false)
    {
        if (TabActiva == null) return;
        await GuardarTabAsync(TabActiva, forzarElegirRuta);
    }

    public async Task GuardarTodoAsync()
    {
        foreach (var tab in Tabs.ToList())
            if (tab.Modificado || string.IsNullOrEmpty(tab.RutaCompleta))
                await GuardarTabAsync(tab, forzarElegirRuta: false);
    }

    async Task GuardarTabAsync(EditorTabViewModel tab, bool forzarElegirRuta)
    {
        if (forzarElegirRuta || string.IsNullOrEmpty(tab.RutaCompleta))
        {
            if (Archivos == null) return;
            var rutaElegida = await Archivos.GuardarComoAsync(tab.Nombre);
            if (string.IsNullOrEmpty(rutaElegida)) return; // cancelado por el usuario

            tab.RutaCompleta = rutaElegida;
            tab.Nombre = Path.GetFileName(rutaElegida);
        }

        try
        {
            File.WriteAllText(tab.RutaCompleta, string.Join('\n', tab.Lineas));
            tab.Modificado = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo guardar {tab.RutaCompleta}: {ex.Message}");
        }
    }

    // ─────────────────────────── delegan a la pestaña activa ───────────────────────────

    public void Copiar(Action<string> alPortapapeles)
    {
        if (TabActiva is { HaySeleccion: true } tab) alPortapapeles(tab.TextoSeleccionado());
    }

    public void Cortar(Action<string> alPortapapeles)
    {
        if (TabActiva is not { HaySeleccion: true } tab) return;
        alPortapapeles(tab.TextoSeleccionado());
        tab.BorrarSeleccion();
    }

    public void Pegar(string texto)
    {
        if (TabActiva == null || string.IsNullOrEmpty(texto)) return;
        TabActiva.InsertarTexto(texto);
    }

    public void Deshacer() => TabActiva?.Deshacer();
    public void Rehacer() => TabActiva?.Rehacer();
    public void IrALinea(int numero) => TabActiva?.IrALinea(numero);
    public void LimpiarEspaciosSobrantes() => TabActiva?.LimpiarEspaciosSobrantes();
}
