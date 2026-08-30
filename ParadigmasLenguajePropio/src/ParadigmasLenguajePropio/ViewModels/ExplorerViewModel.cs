using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Reemplaza ExploradorArchivos.cs: el arbol/scroll/hit-testing/menu-contextual a mano los reemplaza
// el TreeView nativo (ver ExplorerView.axaml); esta clase se queda con la logica de dominio
// (abrir/crear/renombrar/eliminar) que sigue siendo igual de necesaria.
public partial class ExplorerViewModel : ViewModelBase
{
    readonly DialogService _dialogos;

    // se asigna despues de crear la Window (FileDialogService necesita la Window para IStorageProvider)
    public FileDialogService? Archivos { get; set; }

    // conectados por MainWindowViewModel cuando el editor/terminal existan (Fases 5-6)
    public Action<NodoArchivos>? OnAbrirArchivo { get; set; }
    public Action<string, string, string>? OnArchivoRenombrado { get; set; }

    [ObservableProperty]
    private NodoArchivos? _raiz;

    [ObservableProperty]
    private NodoArchivos? _seleccionado;

    [ObservableProperty]
    private bool _hayProyecto;

    public string CarpetaRaiz => Raiz?.RutaCompleta ?? "";

    public ExplorerViewModel(DialogService dialogos)
    {
        _dialogos = dialogos;
    }

    public void AbrirCarpeta(string ruta)
    {
        Raiz = NodoArchivos.CarpetaDesdeDisco(ruta);
        Seleccionado = null;
        HayProyecto = true;
        OnPropertyChanged(nameof(CarpetaRaiz));
    }

    [RelayCommand]
    async Task AbrirCarpetaDialogoAsync()
    {
        if (Archivos == null) return;
        var ruta = await Archivos.ElegirCarpetaAsync();
        if (!string.IsNullOrEmpty(ruta)) AbrirCarpeta(ruta);
    }

    // solo deja de mostrar el proyecto en el explorador: no toca nada en disco ni cierra pestañas del editor
    [RelayCommand]
    void CerrarCarpeta()
    {
        Raiz = null;
        Seleccionado = null;
        HayProyecto = false;
        OnPropertyChanged(nameof(CarpetaRaiz));
    }

    // botón/menú contextual "Crear carpeta": si ya hay proyecto abierto crea adentro de su raíz,
    // si no, arranca uno nuevo (elige ubicación, pide nombre, lo abre)
    [RelayCommand]
    async Task CrearCarpetaAsync()
    {
        if (HayProyecto && Raiz != null) await CrearHijoAsync(Raiz, esCarpeta: true);
        else await CrearProyectoNuevoAsync();
    }

    async Task CrearProyectoNuevoAsync()
    {
        if (Archivos == null) return;
        var ubicacion = await Archivos.ElegirCarpetaAsync();
        if (string.IsNullOrEmpty(ubicacion)) return;

        var nombre = await _dialogos.PromptTextAsync("Crear carpeta", "Nombre de la carpeta:", "nuevo-proyecto");
        if (string.IsNullOrEmpty(nombre)) return;

        try
        {
            var ruta = Path.Combine(ubicacion, nombre);
            Directory.CreateDirectory(ruta);
            AbrirCarpeta(ruta);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo crear la carpeta: {ex.Message}");
        }
    }

    [RelayCommand]
    Task NuevoArchivo(NodoArchivos nodo) => CrearHijoAsync(nodo, esCarpeta: false);

    [RelayCommand]
    Task NuevaCarpeta(NodoArchivos nodo) => CrearHijoAsync(nodo, esCarpeta: true);

    // crea como hijo de la carpeta clickeada, o como hermano si se clickeó un archivo
    async Task CrearHijoAsync(NodoArchivos nodo, bool esCarpeta)
    {
        var carpetaDestino = nodo.EsCarpeta ? nodo : nodo.Padre;
        if (carpetaDestino == null || string.IsNullOrEmpty(carpetaDestino.RutaCompleta)) return;

        var valorInicial = esCarpeta ? "nueva-carpeta" : "nuevo.txt";
        var nombre = await _dialogos.PromptTextAsync(esCarpeta ? "Nueva carpeta" : "Nuevo archivo", "Nombre:", valorInicial);
        if (string.IsNullOrEmpty(nombre)) return;

        var ruta = Path.Combine(carpetaDestino.RutaCompleta, nombre);
        if (File.Exists(ruta) || Directory.Exists(ruta))
        {
            await _dialogos.ShowModalAsync("Aviso", $"Ya existe \"{nombre}\" en esta carpeta.", "Cerrar");
            return;
        }

        try
        {
            if (esCarpeta) Directory.CreateDirectory(ruta);
            else File.WriteAllText(ruta, "");

            carpetaDestino.Expandido = true;
            carpetaDestino.Refrescar();

            if (!esCarpeta)
            {
                var nuevoNodo = carpetaDestino.Hijos.FirstOrDefault(h => h.RutaCompleta == ruta);
                if (nuevoNodo != null) OnAbrirArchivo?.Invoke(nuevoNodo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo crear: {ex.Message}");
        }
    }

    [RelayCommand]
    async Task Renombrar(NodoArchivos nodo)
    {
        if (nodo.Padre == null || string.IsNullOrEmpty(nodo.RutaCompleta)) return;

        var nuevoNombre = await _dialogos.PromptTextAsync("Renombrar", "Nuevo nombre:", nodo.Nombre);
        if (string.IsNullOrEmpty(nuevoNombre) || nuevoNombre == nodo.Nombre) return;

        try
        {
            var carpetaPadre = Path.GetDirectoryName(nodo.RutaCompleta) ?? "";
            var rutaNueva = Path.Combine(carpetaPadre, nuevoNombre);
            if (nodo.EsCarpeta) Directory.Move(nodo.RutaCompleta, rutaNueva);
            else File.Move(nodo.RutaCompleta, rutaNueva);

            var rutaVieja = nodo.RutaCompleta;
            nodo.RutaCompleta = rutaNueva;
            nodo.Nombre = nuevoNombre;
            nodo.Refrescar(); // si tenía hijos ya cargados, quedaron apuntando a la ruta vieja

            OnArchivoRenombrado?.Invoke(rutaVieja, rutaNueva, nuevoNombre);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo renombrar: {ex.Message}");
        }
    }

    [RelayCommand]
    async Task Eliminar(NodoArchivos nodo)
    {
        if (string.IsNullOrEmpty(nodo.RutaCompleta)) return;

        var eleccion = await _dialogos.ShowModalAsync("Confirmar", $"Eliminar \"{nodo.Nombre}\"?", "Si", "No");
        if (eleccion != 0) return;

        try
        {
            if (nodo.EsCarpeta) Directory.Delete(nodo.RutaCompleta, recursive: true);
            else File.Delete(nodo.RutaCompleta);

            if (nodo.Padre != null)
            {
                nodo.Padre.Refrescar();
                if (Seleccionado == nodo) Seleccionado = null;
            }
            else
            {
                CerrarCarpeta(); // era la raíz del proyecto: vuelve al estado "sin carpeta abierta"
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo eliminar: {ex.Message}");
        }
    }

    public void AbrirSiEsArchivo(NodoArchivos nodo)
    {
        if (!nodo.EsCarpeta && !string.IsNullOrEmpty(nodo.RutaCompleta))
            OnAbrirArchivo?.Invoke(nodo);
    }

    // como el "Go to File" de VSCode: filtra por nombre de archivo entre todo lo que hay bajo la carpeta abierta
    public List<ResultadoBusqueda> BuscarArchivosPorNombre(string consulta)
    {
        if (string.IsNullOrEmpty(CarpetaRaiz)) return new();

        try
        {
            return Directory.EnumerateFiles(CarpetaRaiz, "*", SearchOption.AllDirectories)
                .Where(ruta => Path.GetFileName(ruta).Contains(consulta, StringComparison.OrdinalIgnoreCase))
                .Take(10)
                .Select(ruta => new ResultadoBusqueda(
                    Path.GetFileName(ruta),
                    Path.GetRelativePath(CarpetaRaiz, Path.GetDirectoryName(ruta) ?? CarpetaRaiz),
                    ruta))
                .ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo recorrer el proyecto: {ex.Message}");
            return new();
        }
    }
}
