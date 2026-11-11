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

    // ponytail: estatica porque el IDE abre un solo proyecto a la vez
    public static string? RutaPrincipal { get; set; }

    public bool EsPrincipal => !EsCarpeta && RutaPrincipal != null && !string.IsNullOrEmpty(RutaCompleta)
                               && Path.GetFullPath(RutaCompleta) == RutaPrincipal;

    // El arbol se redibuja con cualquier PropertyChanged de sus nodos
    public void AvisarPrincipal()
    {
        OnPropertyChanged(nameof(EsPrincipal));
        foreach (var hijo in Hijos) hijo.AvisarPrincipal();
    }

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

    // Iguala los hijos ya cargados con el disco sin cerrar las carpetas que estaban abiertas
    public void Sincronizar()
    {
        if (!EsCarpeta || !_intentoCarga) return;
        string[] carpetas, archivos;
        try
        {
            carpetas = Directory.GetDirectories(RutaCompleta).OrderBy(r => r).ToArray();
            archivos = Directory.GetFiles(RutaCompleta).OrderBy(r => r).ToArray();
        }
        catch (Exception) { return; }

        var actuales = Hijos.ToDictionary(h => h.RutaCompleta);
        var nuevos = carpetas.Select(r => actuales.GetValueOrDefault(r) is { EsCarpeta: true } h ? h : CarpetaDesdeDisco(r))
            .Concat(archivos.Select(r => actuales.GetValueOrDefault(r) is { EsCarpeta: false } h ? h : Archivo(Path.GetFileName(r), r)))
            .ToList();
        if (!nuevos.SequenceEqual(Hijos))
        {
            Hijos.Clear();
            foreach (var hijo in nuevos) { hijo.Padre = this; Hijos.Add(hijo); }
        }
        foreach (var hijo in nuevos) hijo.Sincronizar();
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
