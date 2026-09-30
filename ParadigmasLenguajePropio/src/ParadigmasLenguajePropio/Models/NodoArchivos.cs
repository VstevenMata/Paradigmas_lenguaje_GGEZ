using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.Models;

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

    public bool EsRaizDeProyecto => Padre == null && !string.IsNullOrEmpty(RutaCompleta);

    bool _intentoCarga;

    // Solo la primera expansion carga los hijos
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
            // Carpeta sin permisos o ilegible: se deja vacia
        }
    }

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

    public static NodoArchivos CarpetaDesdeDisco(string rutaCompleta) => new()
    {
        Nombre = Path.GetFileName(rutaCompleta.TrimEnd(Path.DirectorySeparatorChar)),
        RutaCompleta = rutaCompleta,
        EsCarpeta = true,
    };
}
