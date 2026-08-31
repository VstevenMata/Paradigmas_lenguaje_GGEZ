using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Models;

// Apilado = arriba/abajo, LadoALado = izquierda/derecha
public enum OrientacionSplit { Apilado, LadoALado }

// nodo de un arbol binario de splits del area de edicion: o es una hoja (un panel) o es un
// split con exactamente 2 hijos (A = arriba/izquierda, B = abajo/derecha). El arbol se
// reconstruye entero en cada cambio (nunca se muta in-place), por eso no tiene puntero al
// padre - a diferencia de NodoArchivos (arbol vivo con lazy-load) quedaria obsoleto enseguida.
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
