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
    // no lo recrea, asi que Loaded dispara una sola vez en la practica - la guarda es solo por las dudas
    async void ClaudeTerminalView_Loaded(object? sender, RoutedEventArgs e)
    {
        if (_sesionIniciada) return;
        _sesionIniciada = true;

        string directorio = (DataContext as TerminalViewModel)?.DirectorioActual
            ?? Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        var opciones = new PtyTransportOptions(
            Command: null, // null = shell por defecto del sistema, como al abrir una terminal real
            WorkingDirectory: directorio,
            Environment: null,
            Dimensions: new TerminalSessionDimensions(_terminal.Columns, _terminal.Rows, 1200, 800));

        await _terminal.StartSessionAsync(opciones);
    }
}
