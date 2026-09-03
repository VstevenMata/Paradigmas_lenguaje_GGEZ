using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Reemplaza PanelCentral.cs (la parte de administracion de pestañas: abrir/cerrar/guardar/nuevo).
// El render/scroll/hit-testing del texto en sí vive en EditorTextArea/EditorGutter; la logica de
// edicion linea-a-linea vive en EditorTabViewModel.
public partial class EditorViewModel : ViewModelBase
{
    readonly DialogService _dialogos;

    // la necesita EditorTextArea (via EditorView.axaml) para saber que extensiones de resaltado
    // estan Instalada en cada frame de Render (rainbow-indent, trailing-whitespace, color-lens,
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

    // barra de buscar/reemplazar (BarraBusqueda.cs original) y el cuadro de ir a línea (CuadroIrALinea.cs)
    [ObservableProperty] private bool _buscarVisible;
    [ObservableProperty] private bool _mostrarReemplazo;
    [ObservableProperty] private string _patronBusqueda = "";
    [ObservableProperty] private string _textoReemplazo = "";
    [ObservableProperty] private bool _distingueMayusculas;
    [ObservableProperty] private string _contadorBusqueda = "";
    [ObservableProperty] private bool _irALineaVisible;
    [ObservableProperty] private string _numeroLineaTexto = "";

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
        if (TabActiva != null) { TabActiva.EsActiva = false; TabActiva.CursorCambiado -= AvisarCursor; }
        TabActiva = tab;
        tab.EsActiva = true;
        tab.CursorCambiado += AvisarCursor;
        AvisarCursor();
    }

    void AvisarCursor()
    {
        OnPropertyChanged(nameof(LineaCursor));
        OnPropertyChanged(nameof(ColumnaCursor));
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
            if (TabActiva != null) TabActiva.CursorCambiado -= AvisarCursor;
            TabActiva = null;
            AvisarCursor();
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
