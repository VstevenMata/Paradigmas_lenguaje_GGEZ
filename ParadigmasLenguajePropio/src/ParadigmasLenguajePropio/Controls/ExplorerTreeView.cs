using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ParadigmasLenguajePropio.Converters;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.Controls;

// Reemplaza el arbol de ExploradorArchivos.cs. Se dibuja a mano (mismo patron que EditorTextArea)
// en vez de usar TreeView nativo: ademas de que asi el color del texto queda 100% bajo control
// (TreeViewItem tiene su propio manejo interno de color para :selected que no se pudo
// sobreescribir de forma confiable - el mismo problema que Menu/MenuItem), permite dibujar las
// lineas guia que conectan una carpeta con sus hijos visibles, igual que el original.
public class ExplorerTreeView : Control
{
    const int AltoFila = 22;
    const int IndentacionPorNivel = 14;
    const int LadoIconoCarpeta = 20;
    const int LadoIcono = 16;
    const int FontSize = 12;
    const int MargenIzquierdo = 4;
    const double UmbralDobleClickMs = 400;

    static readonly IPen PlumaGuia = new Pen(new SolidColorBrush(Color.FromRgb(190, 190, 190)));

    public static readonly StyledProperty<NodoArchivos?> RaizProperty =
        AvaloniaProperty.Register<ExplorerTreeView, NodoArchivos?>(nameof(Raiz));

    public static readonly StyledProperty<NodoArchivos?> SeleccionadoProperty =
        AvaloniaProperty.Register<ExplorerTreeView, NodoArchivos?>(nameof(Seleccionado), defaultBindingMode: Avalonia.Data.BindingMode.TwoWay);

    public NodoArchivos? Raiz
    {
        get => GetValue(RaizProperty);
        set => SetValue(RaizProperty, value);
    }

    public NodoArchivos? Seleccionado
    {
        get => GetValue(SeleccionadoProperty);
        set => SetValue(SeleccionadoProperty, value);
    }

    public event Action<NodoArchivos>? ArchivoAbierto; // doble click en un archivo
    public event Action<NodoArchivos, Point>? MenuContextualSolicitado; // click derecho

    NodoArchivos? _nodoUltimoClick;
    long _tiempoUltimoClickMs = -1;

    public ExplorerTreeView()
    {
        Focusable = true;
    }

    static ExplorerTreeView()
    {
        RaizProperty.Changed.AddClassHandler<ExplorerTreeView>((c, e) =>
        {
            // no hace falta desuscribirse del arbol viejo: una vez que Raiz cambia nada vuelve a
            // mutarlo, así que sus eventos no vuelven a dispararse (la escala de un explorador de
            // archivos no justifica la complejidad de rastrear los delegates para sacarlos)
            if (e.NewValue is NodoArchivos nuevo) SuscribirRecursivo(nuevo, c.Refrescar);
            c.Refrescar();
        });
        SeleccionadoProperty.Changed.AddClassHandler<ExplorerTreeView>((c, _) => c.InvalidateVisual());
    }

    // se re-renderiza cuando cambia cualquier propiedad de cualquier nodo visible (expandido,
    // hijos cargados, renombrado). Simplifica no tratar de suscribirse solo a lo estrictamente
    // visible: la cantidad de nodos de un proyecto de este tamaño no lo justifica.
    static void SuscribirRecursivo(NodoArchivos nodo, Action refrescar)
    {
        nodo.PropertyChanged += (_, _) => refrescar();
        nodo.Hijos.CollectionChanged += (_, e) =>
        {
            refrescar();
            // solo los nuevos: re-suscribir a todos duplicaba handlers en cada cambio
            if (e.NewItems != null) foreach (NodoArchivos hijo in e.NewItems) SuscribirRecursivo(hijo, refrescar);
        };
        foreach (var hijo in nodo.Hijos) SuscribirRecursivo(hijo, refrescar);
    }

    void Refrescar()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    // ─────────────────────────── recorrido ───────────────────────────

    IEnumerable<(NodoArchivos Nodo, int Nivel)> NodosVisibles()
    {
        if (Raiz == null) yield break;
        foreach (var item in Recorrer(Raiz, 0)) yield return item;
    }

    static IEnumerable<(NodoArchivos, int)> Recorrer(NodoArchivos nodo, int nivel)
    {
        yield return (nodo, nivel);
        if (nodo.EsCarpeta && nodo.Expandido)
            foreach (var hijo in nodo.Hijos)
                foreach (var item in Recorrer(hijo, nivel + 1))
                    yield return item;
    }

    // ─────────────────────────── medida / render ───────────────────────────

    protected override Size MeasureOverride(Size availableSize) =>
        new(availableSize.Width, NodosVisibles().Count() * AltoFila);

    public override void Render(DrawingContext context)
    {
        if (Raiz == null) return;
        int fila = 0;
        DibujarNodo(context, Raiz, 0, ref fila);
    }

    void DibujarNodo(DrawingContext context, NodoArchivos nodo, int nivel, ref int fila)
    {
        DibujarFila(context, nodo, nivel, fila);
        fila++;

        if (!nodo.EsCarpeta || !nodo.Expandido || nodo.Hijos.Count == 0) return;

        int filaInicioHijos = fila;
        foreach (var hijo in nodo.Hijos) DibujarNodo(context, hijo, nivel + 1, ref fila);

        // guia vertical: conecta la carpeta con todos sus hijos visibles
        double xGuia = MargenIzquierdo + nivel * IndentacionPorNivel + LadoIconoCarpeta / 2.0;
        double yInicio = filaInicioHijos * AltoFila;
        double yFin = fila * AltoFila;
        context.DrawLine(PlumaGuia, new Point(xGuia, yInicio), new Point(xGuia, yFin));
    }

    void DibujarFila(DrawingContext context, NodoArchivos nodo, int nivel, int fila)
    {
        double x = MargenIzquierdo + nivel * IndentacionPorNivel;
        double y = fila * AltoFila;

        bool seleccionado = nodo == Seleccionado;
        if (seleccionado)
        {
            var brushSeleccion = (IBrush?)Application.Current?.FindResource("TemaTitleBarBrush") ?? Brushes.Navy;
            context.FillRectangle(brushSeleccion, new Rect(0, y, Bounds.Width, AltoFila));
        }

        var icono = NodoIconoConverter.ObtenerIcono(nodo);
        int lado = nodo.EsCarpeta ? LadoIconoCarpeta : LadoIcono;
        var destino = new Rect(x, y + (AltoFila - lado) / 2.0, lado, lado);
        context.DrawImage(icono, destino);

        var texto = EditorFont.Formatear(nodo.Nombre, FontSize, seleccionado ? Brushes.White : Brushes.Black);
        double yTexto = y + (AltoFila - texto.Height) / 2.0;
        context.DrawText(texto, new Point(x + LadoIconoCarpeta + 4, yTexto));
    }

    // ─────────────────────────── entrada ───────────────────────────

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        Focus();

        var punto = e.GetPosition(this);
        int indiceFila = (int)(punto.Y / AltoFila);
        var visibles = NodosVisibles().ToList();
        if (indiceFila < 0 || indiceFila >= visibles.Count) return;
        var nodo = visibles[indiceFila].Nodo;

        var propiedades = e.GetCurrentPoint(this).Properties;

        if (propiedades.IsRightButtonPressed)
        {
            Seleccionado = nodo;
            MenuContextualSolicitado?.Invoke(nodo, e.GetPosition(this));
            return;
        }

        if (!propiedades.IsLeftButtonPressed) return;

        long ahora = Environment.TickCount64;
        bool esDobleClick = nodo == _nodoUltimoClick && ahora - _tiempoUltimoClickMs < UmbralDobleClickMs;
        _nodoUltimoClick = nodo;
        _tiempoUltimoClickMs = ahora;

        Seleccionado = nodo;

        if (esDobleClick)
        {
            if (nodo.EsCarpeta) nodo.Expandido = !nodo.Expandido;
            else if (!string.IsNullOrEmpty(nodo.RutaCompleta)) ArchivoAbierto?.Invoke(nodo);
        }
    }
}
