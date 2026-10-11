using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;

namespace ParadigmasLenguajePropio.Controls;

// Codigo GGEZ de solo lectura con los mismos colores del editor en Modo Oscuro
public class CodigoGgez : TextBlock
{
    public static readonly StyledProperty<string?> CodigoProperty =
        AvaloniaProperty.Register<CodigoGgez, string?>(nameof(Codigo));

    public string? Codigo
    {
        get => GetValue(CodigoProperty);
        set => SetValue(CodigoProperty, value);
    }

    static CodigoGgez()
    {
        CodigoProperty.Changed.AddClassHandler<CodigoGgez>((c, _) => c.Colorear());
    }

    public CodigoGgez()
    {
        Foreground = EditorTextArea.TextoOscuro;
    }

    void Colorear()
    {
        var inlines = new InlineCollection();
        bool enBloque = false;
        var lineas = (Codigo ?? "").Split('\n');
        for (int i = 0; i < lineas.Length; i++)
        {
            if (i > 0) inlines.Add(new LineBreak());
            string linea = lineas[i];
            var comentarios = EditorTextArea.BuscarComentarios(linea, ref enBloque);

            // Cada caracter toma el color de su palabra clave o comentario; el resto queda con el texto base
            var colores = new IBrush?[linea.Length];
            foreach (var (inicio, palabra, color) in EditorTextArea.PalabrasColoreadas(linea, comentarios))
                for (int k = 0; k < palabra.Length; k++) colores[inicio + k] = color;
            foreach (var (inicio, fin) in comentarios)
                for (int k = inicio; k < fin; k++) colores[k] = EditorTextArea.ComentarioOscuro;

            int desde = 0;
            for (int k = 1; k <= linea.Length; k++)
            {
                if (k < linea.Length && colores[k] == colores[desde]) continue;
                var run = new Run(linea[desde..k]);
                if (colores[desde] is { } c) run.Foreground = c;
                inlines.Add(run);
                desde = k;
            }
        }
        Inlines = inlines;
    }
}
