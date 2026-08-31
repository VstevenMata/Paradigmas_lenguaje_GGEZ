using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de extensiones: vive en el mismo lugar de pantalla que ExplorerView, se intercambian con
// el toggle de la barra de iconos (ver MainWindow.axaml / MainWindowViewModel.MostrarExtensiones).
// Lista <-> detalle es un solo estado (Seleccionada), no dos vistas separadas.
public partial class ExtensionsViewModel : ViewModelBase
{
    public ObservableCollection<ExtensionInfo> Extensiones { get; } = new()
    {
        new ExtensionInfo("claude", "Claude para GGEZ", "Anthropic", "AI", "#D97757",
            "Asistente de IA integrado: chatea sobre el codigo abierto y aplica los cambios que sugiere.",
            new[] { "Panel de chat lateral", "Lee el archivo activo como contexto", "Aplica ediciones sugeridas al buffer" }),
        new ExtensionInfo("rainbow-indent", "Rainbow Indent Guides", "GGEZ Labs", "RB", "#7C3AED",
            "Colorea las guias de indentacion segun el nivel de anidamiento.",
            new[] { "Un color distinto por nivel de bloque", "Ayuda a ver donde cierra un for/if de un vistazo" }),
        new ExtensionInfo("trailing-whitespace", "Trim Trailing Whitespace", "GGEZ Labs", "WS", "#EF4444",
            "Resalta espacios sobrantes al final de linea y los limpia con un click.",
            new[] { "Resaltado de espacios invisibles", "Comando para limpiar todo el archivo de una" }),
        new ExtensionInfo("color-lens", "Color Lens", "GGEZ Labs", "CL", "#F59E0B",
            "Muestra una muestra de color al lado de cada valor #hex o rgb() en el codigo.",
            new[] { "Detecta #hex, rgb() y rgba()", "Swatch inline junto al texto, sin abrir nada" }),
        new ExtensionInfo("todo-highlight", "TODO Highlight", "GGEZ Labs", "TD", "#3B82F6",
            "Resalta comentarios TODO, FIXME y HACK para que no se pierdan en el archivo.",
            new[] { "Un color de fondo por tipo de comentario", "Se ven de un vistazo al bajar por el archivo" }),
        new ExtensionInfo("bracket-colorizer", "Bracket Pair Colorizer", "GGEZ Labs", "BR", "#10B981",
            "Colorea cada par de parentesis/llaves/corchetes segun su nivel de anidamiento.",
            new[] { "Emparejamiento visual de brackets", "Un color distinto por nivel de anidado" }),
        new ExtensionInfo("ggez-formatter", "GGEZ Formatter", "GGEZ Labs", "FM", "#6B7280",
            "Reformatea codigo GGEZ (indentacion, espacios) reutilizando el lexer del traductor.",
            new[] { "Formatear el archivo completo", "Formatear al guardar (opcional)" }),
    };

    [ObservableProperty]
    private ExtensionInfo? _seleccionada;

    [RelayCommand]
    void VerDetalle(ExtensionInfo ext) => Seleccionada = ext;

    [RelayCommand]
    void VolverALista() => Seleccionada = null;

    [RelayCommand]
    void AlternarInstalacion(ExtensionInfo ext)
    {
        if (ext.Instalada) { ext.Instalada = false; return; }
        InstalarConAnimacion(ext);
    }

    // "descarga fantasma": no hay red de por medio, el codigo de la extension ya esta compilado
    // en la app - esto solo simula una barra de progreso antes de prender Instalada
    static void InstalarConAnimacion(ExtensionInfo ext)
    {
        if (ext.Instalando) return;
        ext.Instalando = true;
        ext.Progreso = 0;
        var inicio = DateTime.UtcNow;

        var timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(16) };
        timer.Tick += (_, _) =>
        {
            double t = (DateTime.UtcNow - inicio).TotalSeconds / 0.9;
            ext.Progreso = Math.Min(1, t);
            if (t < 1) return;

            timer.Stop();
            ext.Instalando = false;
            ext.Instalada = true;
        };
        timer.Start();
    }
}
