using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

public enum ZonaDrop { Arriba, Abajo, Izquierda, Derecha, Centro }

// Paneles del editor en un arbol binario de splits; reenvia la API de EditorViewModel a PanelEnfocado
public partial class EditorAreaViewModel : ViewModelBase
{
    public const int MaxPaneles = 4;

    readonly DialogService _dialogos;
    readonly ExtensionsViewModel _extensiones;
    FileDialogService? _archivos;
    string _carpetaRaiz = "";
    Action<string, string>? _onAbrirArchivoExterno;
    Action<string>? _onRevelarCarpeta;

    [ObservableProperty]
    private NodoPanel _raiz = null!;

    [ObservableProperty]
    private EditorViewModel _panelEnfocado = null!;

    public FileDialogService? Archivos
    {
        get => _archivos;
        set
        {
            _archivos = value;
            foreach (var panel in TodosLosPaneles()) panel.Archivos = value;
        }
    }

    public string CarpetaRaiz
    {
        get => _carpetaRaiz;
        set
        {
            _carpetaRaiz = value;
            foreach (var panel in TodosLosPaneles()) panel.CarpetaRaiz = value;
        }
    }

    public Action<string, string>? OnAbrirArchivoExterno
    {
        get => _onAbrirArchivoExterno;
        set
        {
            _onAbrirArchivoExterno = value;
            foreach (var panel in TodosLosPaneles()) panel.OnAbrirArchivoExterno = value;
        }
    }

    public Action<string>? OnRevelarCarpeta
    {
        get => _onRevelarCarpeta;
        set
        {
            _onRevelarCarpeta = value;
            foreach (var panel in TodosLosPaneles()) panel.OnRevelarCarpeta = value;
        }
    }

    public EditorAreaViewModel(DialogService dialogos, ExtensionsViewModel extensiones)
    {
        _dialogos = dialogos;
        _extensiones = extensiones;
        var primero = CrearPanel();
        Raiz = NodoPanel.Hoja(primero);
        PanelEnfocado = primero;
        primero.PropertyChanged += PanelEnfocado_PropertyChanged;
    }

    EditorViewModel CrearPanel()
    {
        var panel = new EditorViewModel(_dialogos, _extensiones)
        {
            Archivos = _archivos,
            CarpetaRaiz = _carpetaRaiz,
            OnAbrirArchivoExterno = _onAbrirArchivoExterno,
            OnRevelarCarpeta = _onRevelarCarpeta,
        };
        panel.Tabs.CollectionChanged += (_, _) => PodarSiVacio(panel);
        return panel;
    }

    public void RecargarSiCambio(string ruta)
    {
        foreach (var panel in TodosLosPaneles()) panel.RecargarSiCambio(ruta);
    }

    public IEnumerable<EditorViewModel> TodosLosPaneles() => Recorrer(Raiz);

    static IEnumerable<EditorViewModel> Recorrer(NodoPanel nodo)
    {
        if (nodo.EsHoja) { yield return nodo.Panel!; yield break; }
        foreach (var panel in Recorrer(nodo.A!)) yield return panel;
        foreach (var panel in Recorrer(nodo.B!)) yield return panel;
    }

    public int ContarHojas() => TodosLosPaneles().Count();

    public NodoLayoutSesion? GuardarLayout() => ConvertirALayout(Raiz);

    static NodoLayoutSesion? ConvertirALayout(NodoPanel nodo)
    {
        if (nodo.EsHoja)
        {
            var panel = nodo.Panel!;
            var tabs = panel.Tabs.Where(t => !string.IsNullOrEmpty(t.RutaCompleta))
                .Select(t => new TabSesion(t.RutaCompleta, t.FilaCursor, t.ColumnaCursor)).ToList();
            if (tabs.Count == 0) return null;

            int indiceActivo = panel.TabActiva != null ? tabs.FindIndex(t => t.RutaCompleta == panel.TabActiva.RutaCompleta) : -1;
            return new NodoLayoutSesion(new PanelSesion(tabs, indiceActivo), default, null, null);
        }

        var a = ConvertirALayout(nodo.A!);
        var b = ConvertirALayout(nodo.B!);
        return a == null ? b : b == null ? a : new NodoLayoutSesion(null, nodo.Orientacion, a, b);
    }

    public void RestaurarLayout(NodoLayoutSesion? layout)
    {
        if (layout == null) return;
        var nuevaRaiz = ConstruirDesdeLayout(layout);
        if (nuevaRaiz == null) return;

        Raiz = nuevaRaiz;
        EnfocarPanel(TodosLosPaneles().First());
    }

    NodoPanel? ConstruirDesdeLayout(NodoLayoutSesion nodo)
    {
        if (nodo.Panel != null)
        {
            var panel = PoblarPanel(nodo.Panel);
            return panel == null ? null : NodoPanel.Hoja(panel);
        }

        var a = nodo.A != null ? ConstruirDesdeLayout(nodo.A) : null;
        var b = nodo.B != null ? ConstruirDesdeLayout(nodo.B) : null;
        return a == null ? b : b == null ? a : NodoPanel.Dividido(nodo.Orientacion, a, b);
    }

    EditorViewModel? PoblarPanel(PanelSesion guardado)
    {
        var panel = CrearPanel();
        EditorTabViewModel? activa = null;
        for (int i = 0; i < guardado.Tabs.Count; i++)
        {
            var tab = guardado.Tabs[i];
            if (!File.Exists(tab.RutaCompleta)) continue;

            panel.AbrirArchivo(Path.GetFileName(tab.RutaCompleta), tab.RutaCompleta);
            var abierta = panel.Tabs.FirstOrDefault(t => t.RutaCompleta == tab.RutaCompleta);
            abierta?.IrAPosicion(tab.Fila, tab.Columna);
            if (i == guardado.IndiceActivo) activa = abierta;
        }

        if (panel.Tabs.Count == 0) return null;
        if (activa != null) panel.CambiarArchivoActivo(activa);
        return panel;
    }

    static NodoPanel Reemplazar(NodoPanel nodo, EditorViewModel objetivo, NodoPanel reemplazo)
    {
        if (nodo.EsHoja) return ReferenceEquals(nodo.Panel, objetivo) ? reemplazo : nodo;

        var nuevoA = Reemplazar(nodo.A!, objetivo, reemplazo);
        var nuevoB = Reemplazar(nodo.B!, objetivo, reemplazo);
        if (ReferenceEquals(nuevoA, nodo.A) && ReferenceEquals(nuevoB, nodo.B)) return nodo;
        return NodoPanel.Dividido(nodo.Orientacion, nuevoA, nuevoB);
    }

    // null = hoja borrada; el split colapsa y sobrevive el hermano
    static NodoPanel? Podar(NodoPanel nodo, EditorViewModel objetivoVacio)
    {
        if (nodo.EsHoja) return ReferenceEquals(nodo.Panel, objetivoVacio) ? null : nodo;

        var nuevoA = Podar(nodo.A!, objetivoVacio);
        var nuevoB = Podar(nodo.B!, objetivoVacio);

        if (nuevoA == null) return nuevoB;
        if (nuevoB == null) return nuevoA;
        if (ReferenceEquals(nuevoA, nodo.A) && ReferenceEquals(nuevoB, nodo.B)) return nodo;
        return NodoPanel.Dividido(nodo.Orientacion, nuevoA, nuevoB);
    }

    void PodarSiVacio(EditorViewModel panel)
    {
        if (panel.HayArchivosAbiertos || ContarHojas() <= 1) return;

        Raiz = Podar(Raiz, panel)!;
        if (!TodosLosPaneles().Contains(PanelEnfocado))
            EnfocarPanel(TodosLosPaneles().First());
    }

    public void EnfocarPanel(EditorViewModel panel)
    {
        if (ReferenceEquals(PanelEnfocado, panel)) return;
        PanelEnfocado.PropertyChanged -= PanelEnfocado_PropertyChanged;
        PanelEnfocado = panel;
        panel.PropertyChanged += PanelEnfocado_PropertyChanged;
        NotificarTodoElProxy();
    }

    // Los nombres de propiedad coinciden con EditorViewModel a proposito para reenviar PropertyChanged tal cual
    void PanelEnfocado_PropertyChanged(object? sender, PropertyChangedEventArgs e) =>
        OnPropertyChanged(e.PropertyName);

    static readonly string[] PropiedadesProxy =
    {
        nameof(TabActiva), nameof(HayArchivosAbiertos), nameof(Texto),
        nameof(LineaCursor), nameof(ColumnaCursor), nameof(BuscarVisible),
        nameof(MostrarReemplazo), nameof(PatronBusqueda), nameof(TextoReemplazo),
        nameof(DistingueMayusculas), nameof(ContadorBusqueda), nameof(IrALineaVisible),
        nameof(NumeroLineaTexto), nameof(TamanoFuente), nameof(TamanoTab),
    };

    // Cambiar de panel no dispara ningun setter, hay que avisar a mano
    void NotificarTodoElProxy()
    {
        foreach (var propiedad in PropiedadesProxy) OnPropertyChanged(propiedad);
    }

    public EditorTabViewModel? TabActiva => PanelEnfocado.TabActiva;
    public bool HayArchivosAbiertos => PanelEnfocado.HayArchivosAbiertos;
    public string Texto => PanelEnfocado.Texto;
    public int LineaCursor => PanelEnfocado.LineaCursor;
    public int ColumnaCursor => PanelEnfocado.ColumnaCursor;

    public bool BuscarVisible { get => PanelEnfocado.BuscarVisible; set => PanelEnfocado.BuscarVisible = value; }
    public bool MostrarReemplazo { get => PanelEnfocado.MostrarReemplazo; set => PanelEnfocado.MostrarReemplazo = value; }
    public string PatronBusqueda { get => PanelEnfocado.PatronBusqueda; set => PanelEnfocado.PatronBusqueda = value; }
    public string TextoReemplazo { get => PanelEnfocado.TextoReemplazo; set => PanelEnfocado.TextoReemplazo = value; }
    public bool DistingueMayusculas { get => PanelEnfocado.DistingueMayusculas; set => PanelEnfocado.DistingueMayusculas = value; }
    public string ContadorBusqueda => PanelEnfocado.ContadorBusqueda;
    public bool IrALineaVisible { get => PanelEnfocado.IrALineaVisible; set => PanelEnfocado.IrALineaVisible = value; }
    public string NumeroLineaTexto { get => PanelEnfocado.NumeroLineaTexto; set => PanelEnfocado.NumeroLineaTexto = value; }
    public double TamanoFuente { get => PanelEnfocado.TamanoFuente; set => PanelEnfocado.TamanoFuente = value; }
    public int TamanoTab { get => PanelEnfocado.TamanoTab; set => PanelEnfocado.TamanoTab = value; }

    public void NuevoArchivo() => PanelEnfocado.NuevoArchivo();
    public void CambiarZoom(int direccion) => PanelEnfocado.CambiarZoom(direccion);
    public void CambiarTamanoTab(int direccion) => PanelEnfocado.CambiarTamanoTab(direccion);
    public Task GuardarAsync(bool forzarElegirRuta = false) => PanelEnfocado.GuardarAsync(forzarElegirRuta);
    public void Deshacer() => PanelEnfocado.Deshacer();
    public void Rehacer() => PanelEnfocado.Rehacer();
    public void Copiar(Action<string> alPortapapeles) => PanelEnfocado.Copiar(alPortapapeles);
    public void Cortar(Action<string> alPortapapeles) => PanelEnfocado.Cortar(alPortapapeles);
    public void Pegar(string texto) => PanelEnfocado.Pegar(texto);
    public void IrALinea(int numero) => PanelEnfocado.IrALinea(numero);
    public void BuscarSiguiente() => PanelEnfocado.BuscarSiguiente();
    public void BuscarAnterior() => PanelEnfocado.BuscarAnterior();
    public void ReemplazarUno() => PanelEnfocado.ReemplazarUno();
    public void ReemplazarTodo() => PanelEnfocado.ReemplazarTodo();
    public void ConfirmarIrALinea() => PanelEnfocado.ConfirmarIrALinea();

    public async Task GuardarTodoAsync()
    {
        foreach (var panel in TodosLosPaneles().ToList()) await panel.GuardarTodoAsync();
    }

    // Busca en TODOS los paneles para no abrir el mismo archivo como dos buffers distintos
    public void AbrirArchivo(string nombre, string rutaCompleta)
    {
        foreach (var panel in TodosLosPaneles())
        {
            var existente = panel.Tabs.FirstOrDefault(t => t.RutaCompleta == rutaCompleta);
            if (existente == null) continue;

            EnfocarPanel(panel);
            if (existente != panel.TabActiva) panel.CambiarArchivoActivo(existente);
            return;
        }

        PanelEnfocado.AbrirArchivo(nombre, rutaCompleta);
    }

    public void ActualizarRutaSiEstaAbierta(string rutaVieja, string rutaNueva, string nombreNuevo)
    {
        foreach (var panel in TodosLosPaneles()) panel.ActualizarRutaSiEstaAbierta(rutaVieja, rutaNueva, nombreNuevo);
    }

    public void MoverTab(EditorTabViewModel tab, EditorViewModel origen, EditorViewModel destino, ZonaDrop zona)
    {
        if (zona == ZonaDrop.Centro)
        {
            if (ReferenceEquals(origen, destino))
            {
                destino.CambiarArchivoActivo(tab);
                EnfocarPanel(destino);
                return;
            }

            origen.QuitarTab(tab);
            destino.Tabs.Add(tab);
            destino.CambiarArchivoActivo(tab);
            EnfocarPanel(destino);
            return;
        }

        if (ReferenceEquals(origen, destino) && origen.Tabs.Count == 1) return;

        // El tope se chequea ANTES de sacar la pestaña para no perderla si el split se rechaza
        if (ContarHojas() >= MaxPaneles) return;

        origen.QuitarTab(tab);

        var nuevo = CrearPanel();
        nuevo.Tabs.Add(tab);
        nuevo.CambiarArchivoActivo(tab);

        var orientacion = zona is ZonaDrop.Arriba or ZonaDrop.Abajo
            ? OrientacionSplit.Apilado : OrientacionSplit.LadoALado;
        bool nuevoVaPrimero = zona is ZonaDrop.Arriba or ZonaDrop.Izquierda;

        var dividido = NodoPanel.Dividido(orientacion,
            nuevoVaPrimero ? NodoPanel.Hoja(nuevo) : NodoPanel.Hoja(destino),
            nuevoVaPrimero ? NodoPanel.Hoja(destino) : NodoPanel.Hoja(nuevo));

        Raiz = Reemplazar(Raiz, destino, dividido);
        EnfocarPanel(nuevo);
    }
}
