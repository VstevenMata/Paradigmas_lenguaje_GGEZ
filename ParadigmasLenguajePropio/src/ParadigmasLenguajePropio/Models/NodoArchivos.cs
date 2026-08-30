using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.Models;

// Reemplaza NodoArchivos.cs (UI/): mismo modelo (carga perezosa de hijos), ahora bindable
// (ObservableCollection + INotifyPropertyChanged) para que TreeView reaccione solo.
public partial class NodoArchivos : ObservableObject
{
    [ObservableProperty]
    private string _nombre = "";

    [ObservableProperty]
    private string _rutaCompleta = "";

    [ObservableProperty]
    private bool _esCarpeta;

    [ObservableProperty]
    private bool _expandido;

    public ObservableCollection<NodoArchivos> Hijos { get; } = new();

    public NodoArchivos? Padre { get; set; }

    // raíz del proyecto abierto (no la carpeta placeholder "sin proyecto"): no se puede renombrar/mover
    public bool EsRaizDeProyecto => Padre == null && !string.IsNullOrEmpty(RutaCompleta);

    bool _intentoCarga;

    // al expandir por primera vez dispara la carga perezosa; togglear Expandido a mano (ej. tras
    // renombrar/eliminar hijos) no repite la carga porque _intentoCarga ya quedó en true
    partial void OnExpandidoChanged(bool value)
    {
        if (value) Expandir();
    }

    public void Expandir()
    {
        if (!EsCarpeta || _intentoCarga || string.IsNullOrEmpty(RutaCompleta)) return;
        _intentoCarga = true;

        try
        {
            foreach (var ruta in Directory.GetDirectories(RutaCompleta).OrderBy(r => r))
            {
                var hijo = CarpetaDesdeDisco(ruta);
                hijo.Padre = this;
                Hijos.Add(hijo);
            }

            foreach (var ruta in Directory.GetFiles(RutaCompleta).OrderBy(r => r))
            {
                var hijo = Archivo(Path.GetFileName(ruta), ruta);
                hijo.Padre = this;
                Hijos.Add(hijo);
            }
        }
        catch (Exception)
        {
            // evita que falle si la carpeta no tiene permisos o hijos legibles
        }
    }

    // vuelve a leer del disco: usarlo después de crear/renombrar/eliminar algo adentro
    public void Refrescar()
    {
        Hijos.Clear();
        _intentoCarga = false;
        if (Expandido) Expandir();
    }

    public static NodoArchivos Archivo(string nombre, string rutaCompleta = "") => new()
    {
        Nombre = nombre,
        RutaCompleta = rutaCompleta,
    };

    // los hijos se leen recién al expandir por primera vez (lazy load)
    public static NodoArchivos CarpetaDesdeDisco(string rutaCompleta) => new()
    {
        Nombre = Path.GetFileName(rutaCompleta.TrimEnd(Path.DirectorySeparatorChar)),
        RutaCompleta = rutaCompleta,
        EsCarpeta = true,
    };
}
