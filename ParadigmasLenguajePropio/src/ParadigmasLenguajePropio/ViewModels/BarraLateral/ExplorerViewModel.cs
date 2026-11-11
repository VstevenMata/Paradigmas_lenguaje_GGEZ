using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Logica de dominio del explorador: abrir, crear, renombrar y eliminar
public partial class ExplorerViewModel : ViewModelBase
{
    readonly DialogService _dialogos;

    public FileDialogService? Archivos { get; set; }

    public Action<NodoArchivos>? OnAbrirArchivo { get; set; }
    public Action<string, string, string>? OnArchivoRenombrado { get; set; }

    [ObservableProperty]
    private NodoArchivos? _raiz;

    [ObservableProperty]
    private NodoArchivos? _seleccionado;

    [ObservableProperty]
    private bool _hayProyecto;

    public string CarpetaRaiz => Raiz?.RutaCompleta ?? "";

    // Guarda la ruta relativa del archivo principal dentro del proyecto
    const string ArchivoProyecto = ".ggezproyecto";

    public string? RutaPrincipal => NodoArchivos.RutaPrincipal;

    void CargarPrincipal()
    {
        NodoArchivos.RutaPrincipal = null;
        try
        {
            var config = Path.Combine(CarpetaRaiz, ArchivoProyecto);
            var relativa = File.Exists(config) ? File.ReadAllText(config).Trim() : "";
            if (relativa.Length > 0) NodoArchivos.RutaPrincipal = Path.GetFullPath(Path.Combine(CarpetaRaiz, relativa));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo leer {ArchivoProyecto}: {ex.Message}");
        }
    }

    [RelayCommand]
    void MarcarPrincipal(NodoArchivos nodo)
    {
        if (nodo.EsCarpeta || string.IsNullOrEmpty(CarpetaRaiz)) return;
        NodoArchivos.RutaPrincipal = Path.GetFullPath(nodo.RutaCompleta);
        try
        {
            var relativa = Path.GetRelativePath(CarpetaRaiz, nodo.RutaCompleta).Replace('\\', '/');
            File.WriteAllText(Path.Combine(CarpetaRaiz, ArchivoProyecto), relativa);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo guardar el archivo principal: {ex.Message}");
        }
        Raiz?.AvisarPrincipal();
    }

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
        CargarPrincipal();
    }

    [RelayCommand]
    async Task AbrirCarpetaDialogoAsync()
    {
        if (Archivos == null) return;
        var ruta = await Archivos.ElegirCarpetaAsync();
        if (!string.IsNullOrEmpty(ruta)) AbrirCarpeta(ruta);
    }

    // No toca nada en disco ni cierra pestañas del editor
    [RelayCommand]
    void CerrarCarpeta()
    {
        NodoArchivos.RutaPrincipal = null;
        Raiz = null;
        Seleccionado = null;
        HayProyecto = false;
        OnPropertyChanged(nameof(CarpetaRaiz));
    }

    // Con proyecto abierto crea dentro de la raiz; si no, arranca un proyecto nuevo
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
            bool eraPrincipal = nodo.EsPrincipal;
            nodo.RutaCompleta = rutaNueva;
            nodo.Nombre = nuevoNombre;
            nodo.Refrescar(); // sus hijos cargados quedaron apuntando a la ruta vieja
            if (eraPrincipal) MarcarPrincipal(nodo);

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
                CerrarCarpeta();
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

    public void RevelarCarpeta(string rutaCompleta)
    {
        if (Raiz == null || string.IsNullOrEmpty(rutaCompleta)) return;

        var relativa = Path.GetRelativePath(Raiz.RutaCompleta, rutaCompleta);
        if (relativa.StartsWith("..")) return;

        var actual = Raiz;
        if (relativa != ".")
        {
            foreach (var parte in relativa.Split(Path.DirectorySeparatorChar))
            {
                actual.Expandido = true;
                var siguiente = actual.Hijos.FirstOrDefault(h => h.Nombre == parte);
                if (siguiente == null) return;
                actual = siguiente;
            }
        }

        actual.Expandido = true;
        Seleccionado = actual;
    }

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
