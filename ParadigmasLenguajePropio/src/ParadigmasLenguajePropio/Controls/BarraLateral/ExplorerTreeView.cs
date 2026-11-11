using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using ParadigmasLenguajePropio.Converters;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.Controls;

// Dibujado a mano: TreeViewItem no deja controlar el color de :selected y asi se dibujan las lineas guia
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

    public event Action<NodoArchivos>? ArchivoAbierto;
    public event Action<NodoArchivos, Point>? MenuContextualSolicitado;

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
            // Sin desuscribir del arbol viejo: una vez reemplazado no vuelve a mutar
            if (e.NewValue is NodoArchivos nuevo) SuscribirRecursivo(nuevo, c.Refrescar);
            c.Refrescar();
        });
        SeleccionadoProperty.Changed.AddClassHandler<ExplorerTreeView>((c, _) => c.InvalidateVisual());
    }

    static void SuscribirRecursivo(NodoArchivos nodo, Action refrescar)
    {
        nodo.PropertyChanged += (_, _) => refrescar();
        nodo.Hijos.CollectionChanged += (_, e) =>
        {
            refrescar();
            // Solo los nuevos: re-suscribir a todos duplicaba handlers
            if (e.NewItems != null) foreach (NodoArchivos hijo in e.NewItems) SuscribirRecursivo(hijo, refrescar);
        };
        foreach (var hijo in nodo.Hijos) SuscribirRecursivo(hijo, refrescar);
    }

    void Refrescar()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

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

        var texto = EditorFont.Formatear(nodo.EsPrincipal ? nodo.Nombre + "  (principal)" : nodo.Nombre, FontSize, seleccionado ? Brushes.White : Brushes.Black);
        double yTexto = y + (AltoFila - texto.Height) / 2.0;
        context.DrawText(texto, new Point(x + LadoIconoCarpeta + 4, yTexto));
    }

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
