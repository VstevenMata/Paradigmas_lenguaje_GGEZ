using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
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
        // Tunnel: el TerminalControl consume el clic y las flechas no le llegaban (no se podia elegir en los menus de claude)
        AddHandler(PointerPressedEvent, (_, _) => (DataContext as TerminalInstanceViewModel)?.OnActivar?.Invoke(), RoutingStrategies.Tunnel);
        AddHandler(KeyDownEvent, EnviarTeclaEspecial, RoutingStrategies.Tunnel);
    }

    // Este control y la terminal simulada se instancian en cada columna: se revisa EsReal antes de abrir un shell real
    void ClaudeTerminalView_Loaded(object? sender, RoutedEventArgs e) => _ = IniciarSesionAsync();

    // Una simulada que pasa a real (se instalo Claude) abre su shell en ese momento
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        if (DataContext is TerminalInstanceViewModel instancia)
            instancia.PropertyChanged += (_, args) =>
            {
                // Background: espera a que se mida ya visible, si no la PTY arranca con 0 columnas
                if (args.PropertyName == nameof(TerminalInstanceViewModel.EsReal) && IsLoaded)
                    Dispatcher.UIThread.Post(() => _ = IniciarSesionAsync(), DispatcherPriority.Background);
            };
    }

    void EnviarTeclaEspecial(object? sender, KeyEventArgs e)
    {
        if (e.KeyModifiers is not (KeyModifiers.None or KeyModifiers.Shift)) return;
        string? secuencia = (e.Key, e.KeyModifiers) switch
        {
            (Key.Up, KeyModifiers.None) => "\e[A",
            (Key.Down, KeyModifiers.None) => "\e[B",
            (Key.Right, KeyModifiers.None) => "\e[C",
            (Key.Left, KeyModifiers.None) => "\e[D",
            (Key.Enter, KeyModifiers.None) => "\r",
            (Key.Escape, KeyModifiers.None) => "\e",
            (Key.Tab, KeyModifiers.None) => "\t",
            (Key.Tab, KeyModifiers.Shift) => "\e[Z",
            _ => null,
        };
        if (secuencia == null) return;
        _terminal.SendInput(secuencia);
        e.Handled = true;
    }

    async Task IniciarSesionAsync()
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

        try
        {
            await _terminal.StartSessionAsync(opciones);
            if (DataContext is TerminalInstanceViewModel { ComandoInicial: { } comando } conComando)
            {
                conComando.ComandoInicial = null;
                _terminal.SendInput(comando + "\r");
            }
        }
        // Si la PTY no abre (p. ej. ConPTY en Windows) vuelve a la simulada mostrando el motivo, en vez de quedar en blanco
        catch (Exception ex) when (DataContext is TerminalInstanceViewModel instancia)
        {
            instancia.EsReal = false;
            instancia.EscribirAviso($"No se pudo abrir la terminal real: {ex.Message}");
        }
    }
}
