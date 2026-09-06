using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.Models;

// una fila del menu que abre un segmento del breadcrumb
public partial class NodoBreadcrumb : ObservableObject
{
    public string Nombre { get; }
    public string RutaCompleta { get; }
    public bool EsCarpeta { get; }

    public string NombreMostrado => EsCarpeta ? Nombre + "/" : Nombre;

    [ObservableProperty]
    private bool _esResaltado;

    public NodoBreadcrumb(string nombre, string rutaCompleta, bool esCarpeta)
    {
        Nombre = nombre;
        RutaCompleta = rutaCompleta;
        EsCarpeta = esCarpeta;
    }
}
