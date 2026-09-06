using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.ViewModels;

// Un segmento clickeable del breadcrumb: al abrirse lista el contenido de CarpetaCompleta.
public partial class BreadcrumbSegmentoViewModel : ObservableObject
{
    public string Texto { get; }
    public string CarpetaCompleta { get; }
    public bool MostrarSeparadorAntes { get; }

    public Action<NodoBreadcrumb>? AlElegirArchivo { get; set; }
    public Action<NodoBreadcrumb>? AlElegirCarpeta { get; set; }

    [ObservableProperty] private bool _esAbierto;
    public ObservableCollection<NodoBreadcrumb> Items { get; } = new();

    public BreadcrumbSegmentoViewModel(string texto, string carpetaCompleta, bool mostrarSeparadorAntes)
    {
        Texto = texto;
        CarpetaCompleta = carpetaCompleta;
        MostrarSeparadorAntes = mostrarSeparadorAntes;
    }

    partial void OnEsAbiertoChanged(bool value)
    {
        if (value) CargarItems();
    }

    public void Elegir(NodoBreadcrumb nodo)
    {
        if (nodo.EsCarpeta) AlElegirCarpeta?.Invoke(nodo);
        else AlElegirArchivo?.Invoke(nodo);
        EsAbierto = false;
    }

    void CargarItems()
    {
        Items.Clear();
        if (string.IsNullOrEmpty(CarpetaCompleta)) return;

        try
        {
            var carpetas = Directory.EnumerateDirectories(CarpetaCompleta)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(ruta => new NodoBreadcrumb(Path.GetFileName(ruta), ruta, esCarpeta: true));

            var archivos = Directory.EnumerateFiles(CarpetaCompleta)
                .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
                .Select(ruta => new NodoBreadcrumb(Path.GetFileName(ruta), ruta, esCarpeta: false));

            foreach (var nodo in carpetas.Concat(archivos)) Items.Add(nodo);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo listar {CarpetaCompleta}: {ex.Message}");
        }
    }
}
