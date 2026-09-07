using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// donde soltar una pestaña arrastrada: Arriba/Abajo/Izquierda/Derecha la parten en dos (armando
// una grilla, no solo apilando), Centro la mueve sin partir (o solo la reactiva si se suelta
// sobre su propio panel)
public enum ZonaDrop { Arriba, Abajo, Izquierda, Derecha, Centro }

// Dueña de varios EditorViewModel ("paneles") organizados en un arbol binario de splits (ver
// Models/NodoPanel), para el split tipo VS Code: arrastrar una pestaña arriba/abajo/izquierda/
// derecha del area central arma una grilla de hasta MaxPaneles archivos a la vez, en cualquier
// combinacion (no solo una fila o una columna). Expone la MISMA superficie publica que
// EditorViewModel tenia cuando era el unico panel, reenviando todo a PanelEnfocado - asi
// MainWindowViewModel.Editor no cambia en ningun otro lugar que lo use.
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

    // ─────────────────────────── recorrido del arbol ───────────────────────────

    public IEnumerable<EditorViewModel> TodosLosPaneles() => Recorrer(Raiz);

    static IEnumerable<EditorViewModel> Recorrer(NodoPanel nodo)
    {
        if (nodo.EsHoja) { yield return nodo.Panel!; yield break; }
        foreach (var panel in Recorrer(nodo.A!)) yield return panel;
        foreach (var panel in Recorrer(nodo.B!)) yield return panel;
    }

    public int ContarHojas() => TodosLosPaneles().Count();

    // reemplaza la hoja que contiene "objetivo" por "reemplazo"; reusa las ramas que no
    // cambiaron (no reconstruye el arbol entero, solo el camino hasta la hoja tocada)
    static NodoPanel Reemplazar(NodoPanel nodo, EditorViewModel objetivo, NodoPanel reemplazo)
    {
        if (nodo.EsHoja) return ReferenceEquals(nodo.Panel, objetivo) ? reemplazo : nodo;

        var nuevoA = Reemplazar(nodo.A!, objetivo, reemplazo);
        var nuevoB = Reemplazar(nodo.B!, objetivo, reemplazo);
        if (ReferenceEquals(nuevoA, nodo.A) && ReferenceEquals(nuevoB, nodo.B)) return nodo;
        return NodoPanel.Dividido(nodo.Orientacion, nuevoA, nuevoB);
    }

    // null = "esta hoja se borro"; si un hijo de un split vuelve null, el split colapsa y
    // sobrevive directamente el hermano (el nodo split desaparece, no queda un split de 1 solo)
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

        Raiz = Podar(Raiz, panel)!; // ContarHojas()>1 garantiza que no era la unica hoja
        if (!TodosLosPaneles().Contains(PanelEnfocado))
            EnfocarPanel(TodosLosPaneles().First());
    }

    // ─────────────────────────── foco ───────────────────────────

    public void EnfocarPanel(EditorViewModel panel)
    {
        if (ReferenceEquals(PanelEnfocado, panel)) return;
        PanelEnfocado.PropertyChanged -= PanelEnfocado_PropertyChanged;
        PanelEnfocado = panel;
        panel.PropertyChanged += PanelEnfocado_PropertyChanged;
        NotificarTodoElProxy();
    }

    // los nombres de propiedad son identicos entre EditorViewModel y EditorAreaViewModel a
    // proposito - es lo que permite reenviar el PropertyChanged tal cual, sin mapear nombre por
    // nombre, para todo lo que cambia SIN que cambie el panel enfocado (tipear en la busqueda,
    // mover el cursor, etc.)
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

    // cambiar de panel enfocado no dispara por si solo el PropertyChanged de estas propiedades
    // (el valor "de afuera" cambio, pero ningun setter se llamo) - hay que avisarlo a mano
    void NotificarTodoElProxy()
    {
        foreach (var propiedad in PropiedadesProxy) OnPropertyChanged(propiedad);
    }

    // ─────────────────────────── proxy: delega en PanelEnfocado ───────────────────────────

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
    public void LimpiarEspaciosSobrantes() => PanelEnfocado.LimpiarEspaciosSobrantes();

    // a diferencia del resto de los proxies: guarda TODOS los paneles, no solo el enfocado
    public async Task GuardarTodoAsync()
    {
        foreach (var panel in TodosLosPaneles().ToList()) await panel.GuardarTodoAsync();
    }

    // dedupe GLOBAL: a diferencia de EditorViewModel.AbrirArchivo (que solo mira sus propios
    // Tabs), aca hay que escanear TODOS los paneles antes de decidir que el archivo no esta
    // abierto en ningun lado - si no, dos paneles distintos podrian terminar con el mismo
    // archivo abierto dos veces como dos buffers independientes
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

    // ─────────────────────────── arrastrar pestañas entre paneles ───────────────────────────

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

        // partir en dos no tiene sentido si es la unica pestaña de su propio panel arrastrada
        // sobre si mismo: dejaria un panel vacio al lado de uno con el mismo contenido
        if (ReferenceEquals(origen, destino) && origen.Tabs.Count == 1) return;

        // el chequeo del tope va ANTES de sacar la pestaña de origen: si se chequeara despues,
        // un split rechazado igual habria arrancado la pestaña de origen sin ponerla en
        // ningun lado. Es una aproximacion conservadora: si origen quedara vacio y se podara,
        // el conteo neto podria no subir - pero cubrir ese caso no vale la complejidad.
        if (ContarHojas() >= MaxPaneles) return;

        origen.QuitarTab(tab); // puede podar `origen` del arbol de forma sincronica

        var nuevo = CrearPanel();
        nuevo.Tabs.Add(tab);
        nuevo.CambiarArchivoActivo(tab);

        var orientacion = zona is ZonaDrop.Arriba or ZonaDrop.Abajo
            ? OrientacionSplit.Apilado : OrientacionSplit.LadoALado;
        bool nuevoVaPrimero = zona is ZonaDrop.Arriba or ZonaDrop.Izquierda;

        var dividido = NodoPanel.Dividido(orientacion,
            nuevoVaPrimero ? NodoPanel.Hoja(nuevo) : NodoPanel.Hoja(destino),
            nuevoVaPrimero ? NodoPanel.Hoja(destino) : NodoPanel.Hoja(nuevo));

        // referencial, no por indice: encuentra "destino" donde este, sin importar si QuitarTab
        // acaba de podar a "origen" (la identidad de destino no cambia con eso)
        Raiz = Reemplazar(Raiz, destino, dividido);
        EnfocarPanel(nuevo);
    }
}
