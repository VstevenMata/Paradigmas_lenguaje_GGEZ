using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Controls;

// Reemplaza la mitad "números de línea" de PanelCentral.DibujarTexto: se dibujan aparte de
// EditorTextArea para poder vivir fuera de su ScrollViewer horizontal (los números no se
// desplazan de lado, solo verticalmente, junto con el texto).
public class EditorGutter : Control
{
    const int MargenNumeros = 8; // 4px de aire a cada lado del numero mas ancho posible

    // gris suave para distinguir la columna de numeros del blanco del area de texto, igual que el
    // gutter de VSCode - un poco mas oscuro que el fondo del minimapa (246,246,246) porque aca hay
    // texto chico encima (los numeros) y conviene mas contraste que en el minimapa
    static readonly IBrush FondoGutter = new SolidColorBrush(Color.FromRgb(238, 238, 238));

    public EditorGutter()
    {
        // defensivo: si por lo que sea un numero midiera mas de lo que AnchoNumeros calculo (fuente
        // no tabular, redondeos), que se recorte en vez de pintarse fuera de esta columna y de ahi
        // fuera del panel central - es justo el bug que reemplaza este ancho dinamico (ver abajo)
        ClipToBounds = true;
    }

    // antes un ancho fijo de 40px: a un zoom alto, un numero de 3 cifras a ese tamaño de fuente
    // mide mas que eso, y como Render() lo alinea a la derecha (AnchoNumeros - formatted.Width),
    // el resultado era una X negativa - el numero se dibujaba empezando a la izquierda de esta
    // columna, es decir se salia del panel central. Se calcula a partir del numero MAS ANCHO que
    // puede aparecer (la ultima linea: mas digitos que "999" si el archivo supera 999 lineas) al
    // TamanoFuente actual, asi que escala solo con el zoom y con el largo del archivo.
    double AnchoNumeros
    {
        get
        {
            int digitos = Math.Max(3, (Tab?.Lineas.Count ?? 0).ToString().Length);
            return EditorFont.MedirPrefijo(new string('9', digitos), TamanoFuente) + MargenNumeros;
        }
    }

    public static readonly StyledProperty<EditorTabViewModel?> TabProperty =
        AvaloniaProperty.Register<EditorGutter, EditorTabViewModel?>(nameof(Tab));

    public EditorTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    // zoom (ver EditorTextArea.TamanoFuente): mismo StyledProperty bindeado desde el mismo lugar
    // en EditorView.axaml, y AltoLinea derivado con EditorFont.AltoLinea - la misma cuenta que usa
    // EditorTextArea, para que los numeros de linea nunca se desalineen del texto al hacer zoom.
    public static readonly StyledProperty<double> TamanoFuenteProperty =
        AvaloniaProperty.Register<EditorGutter, double>(nameof(TamanoFuente), 18.0);

    public double TamanoFuente
    {
        get => GetValue(TamanoFuenteProperty);
        set => SetValue(TamanoFuenteProperty, value);
    }

    public double AltoLinea => EditorFont.AltoLinea(TamanoFuente);

    // mismo par que EditorTextArea.DesplazamientoY/AltoVisible, sincronizado desde el mismo lugar
    // (EditorView.axaml.cs, en cada ScrollChanged) - sin esto Render() dibujaba los numeros de
    // TODAS las lineas del archivo en cada frame, aunque el gutter solo muestre unas pocas decenas
    public static readonly StyledProperty<double> DesplazamientoYProperty =
        AvaloniaProperty.Register<EditorGutter, double>(nameof(DesplazamientoY));

    public double DesplazamientoY
    {
        get => GetValue(DesplazamientoYProperty);
        set => SetValue(DesplazamientoYProperty, value);
    }

    public static readonly StyledProperty<double> AltoVisibleProperty =
        AvaloniaProperty.Register<EditorGutter, double>(nameof(AltoVisible));

    public double AltoVisible
    {
        get => GetValue(AltoVisibleProperty);
        set => SetValue(AltoVisibleProperty, value);
    }

    static EditorGutter()
    {
        TabProperty.Changed.AddClassHandler<EditorGutter>((c, e) =>
        {
            if (e.OldValue is EditorTabViewModel viejo) viejo.ContenidoCambiado -= c.Refrescar;
            if (e.NewValue is EditorTabViewModel nuevo) nuevo.ContenidoCambiado += c.Refrescar;
            c.Refrescar();
        });

        TamanoFuenteProperty.Changed.AddClassHandler<EditorGutter>((c, _) => c.Refrescar());

        // scroll: no cambia el tamaño total del gutter, solo que rango dibuja - alcanza con
        // InvalidateVisual()
        DesplazamientoYProperty.Changed.AddClassHandler<EditorGutter>((c, _) => c.InvalidateVisual());
        AltoVisibleProperty.Changed.AddClassHandler<EditorGutter>((c, _) => c.InvalidateVisual());
    }

    void Refrescar()
    {
        InvalidateMeasure();
        InvalidateVisual();
    }

    protected override Size MeasureOverride(Size availableSize) =>
        new(AnchoNumeros, (Tab?.Lineas.Count ?? 0) * AltoLinea);

    public override void Render(DrawingContext context)
    {
        if (Tab == null) return;

        context.FillRectangle(FondoGutter, new Rect(Bounds.Size));

        // cacheado: AnchoNumeros vuelve a medir un string cada vez que se lee, no hace falta
        // repetirlo una vez por linea (ya no cambia dentro de este frame)
        double anchoNumeros = AnchoNumeros;
        double margenDerecho = MargenNumeros / 2.0;
        double altoLinea = AltoLinea;

        // mismo viewport que EditorTextArea.Render (ver comentario ahi): solo los numeros que
        // realmente se ven, no el archivo entero en cada frame
        int primeraLinea = 0;
        int ultimaLinea = Tab.Lineas.Count - 1;
        if (AltoVisible > 0)
        {
            primeraLinea = Math.Max(0, (int)(DesplazamientoY / altoLinea) - 2);
            ultimaLinea = Math.Min(Tab.Lineas.Count - 1, (int)((DesplazamientoY + AltoVisible) / altoLinea) + 2);
        }

        for (int i = primeraLinea; i <= ultimaLinea; i++)
        {
            string numero = (i + 1).ToString("D3");
            var formatted = EditorFont.Formatear(numero, TamanoFuente, Brushes.DarkGray);
            double y = i * altoLinea;
            context.DrawText(formatted, new Point(anchoNumeros - formatted.Width - margenDerecho, y));
        }
    }
}
