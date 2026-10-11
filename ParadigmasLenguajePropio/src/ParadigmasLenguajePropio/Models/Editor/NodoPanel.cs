using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Models;

public enum OrientacionSplit { Apilado, LadoALado }

// Hoja (un panel) o split con 2 hijos (A = arriba/izquierda); inmutable, se reconstruye en cada cambio
public sealed record NodoPanel
{
    public EditorViewModel? Panel { get; }
    public OrientacionSplit Orientacion { get; }
    public NodoPanel? A { get; }
    public NodoPanel? B { get; }

    public bool EsHoja => Panel != null;

    NodoPanel(EditorViewModel? panel, OrientacionSplit orientacion, NodoPanel? a, NodoPanel? b) =>
        (Panel, Orientacion, A, B) = (panel, orientacion, a, b);

    public static NodoPanel Hoja(EditorViewModel panel) => new(panel, default, null, null);

    public static NodoPanel Dividido(OrientacionSplit orientacion, NodoPanel a, NodoPanel b) =>
        new(null, orientacion, a, b);
}
