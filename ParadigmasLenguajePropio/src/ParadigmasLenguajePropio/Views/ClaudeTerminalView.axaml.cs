using Avalonia.Controls;
using Avalonia.Interactivity;
using ParadigmasLenguajePropio.ViewModels;
using RoyalTerminal.Avalonia.Controls;
using RoyalTerminal.Terminal;

namespace ParadigmasLenguajePropio.Views;

public partial class ClaudeTerminalView : UserControl
{
    readonly TerminalControl _terminal;
    bool _sesionIniciada;

    public ClaudeTerminalView()
    {
        InitializeComponent();
        _terminal = this.FindControl<TerminalControl>("PART_Terminal")!;
        Loaded += ClaudeTerminalView_Loaded;
    }

    // Este control y la terminal simulada se instancian en cada columna: se revisa EsReal antes de abrir un shell real
    async void ClaudeTerminalView_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_sesionIniciada) return;
        if (DataContext is TerminalInstanceViewModel { EsReal: false }) return;
        _sesionIniciada = true;

        string directorio = DataContext switch
        {
            TerminalInstanceViewModel instancia => instancia.DirectorioActual,
            TerminalViewModel panel => panel.DirectorioActual,
            _ => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        var opciones = new PtyTransportOptions(
            Command: null, // shell por defecto del sistema
            WorkingDirectory: directorio,
            Environment: null,
            Dimensions: new TerminalSessionDimensions(_terminal.Columns, _terminal.Rows, 1200, 800));

        await _terminal.StartSessionAsync(opciones);
    }
}
