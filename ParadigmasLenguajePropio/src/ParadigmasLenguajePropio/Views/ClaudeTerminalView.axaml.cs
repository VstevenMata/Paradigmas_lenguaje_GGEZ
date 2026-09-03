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

    // una sola sesion por vida del control: cambiar de pestaña solo lo oculta (ver TerminalView.axaml),
    // no lo recrea, asi que Loaded dispara una sola vez en la practica - la guarda es solo por las dudas.
    // Ojo: este control vive DENTRO de cada columna del panel de terminales (ver TerminalView.axaml)
    // junto con TerminalInstanceView (la simulada), las dos se instancian aunque solo una se muestre
    // (IsVisible) - por eso hay que revisar EsReal antes de gastar un shell real de verdad.
    async void ClaudeTerminalView_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_sesionIniciada) return;
        if (DataContext is TerminalInstanceViewModel { EsReal: false }) return; // esta columna es simulada, no real
        _sesionIniciada = true;

        string directorio = DataContext switch
        {
            TerminalInstanceViewModel instancia => instancia.DirectorioActual,
            TerminalViewModel panel => panel.DirectorioActual,
            _ => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        };

        var opciones = new PtyTransportOptions(
            Command: null, // null = shell por defecto del sistema, como al abrir una terminal real
            WorkingDirectory: directorio,
            Environment: null,
            Dimensions: new TerminalSessionDimensions(_terminal.Columns, _terminal.Rows, 1200, 800));

        await _terminal.StartSessionAsync(opciones);
    }
}
