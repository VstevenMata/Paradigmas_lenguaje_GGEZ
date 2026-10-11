using System.Collections.Specialized;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class TerminalInstanceView : UserControl
{
    TerminalInstanceViewModel? ViewModel => DataContext as TerminalInstanceViewModel;

    ScrollViewer _scroll = null!;
    TextBox _entrada = null!;
    bool _ancladoAlFinal = true;
    bool _scrollPendiente;

    public TerminalInstanceView()
    {
        InitializeComponent();

        _scroll = this.FindControl<ScrollViewer>("PART_Scroll")!;
        _entrada = this.FindControl<TextBox>("PART_Entrada")!;

        _scroll.ScrollChanged += (_, _) =>
            _ancladoAlFinal = _scroll.Offset.Y >= _scroll.Extent.Height - _scroll.Viewport.Height - 2;

        DataContextChanged += (_, _) =>
        {
            if (ViewModel is not { } vm) return;
            vm.LineasTerminal.CollectionChanged += SeguirAlFinalSiCorresponde;
        };
    }

    void SeguirAlFinalSiCorresponde(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // Un comando ruidoso agrega cientos de lineas por tick: un solo ScrollToEnd alcanza
        if (!_ancladoAlFinal || _scrollPendiente) return;
        _scrollPendiente = true;
        Dispatcher.UIThread.Post(() => { _scrollPendiente = false; _scroll.ScrollToEnd(); }, DispatcherPriority.Background);
    }

    // La caja de texto real mide pocos pixeles vacia: clickear el fondo la enfoca y activa la instancia
    void Fondo_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        ViewModel?.OnActivar?.Invoke();
        _entrada.Focus();
    }

    void Entrada_GotFocus(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        vm.TieneFoco = true;
        vm.OnActivar?.Invoke();
    }

    void Entrada_LostFocus(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is { } vm) vm.TieneFoco = false;
    }

    void Entrada_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;

        bool ctrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool shift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);

        // Ctrl+Shift+C antes que Ctrl+C, que corta el proceso
        if (ctrl && shift && e.Key == Key.C) { _ = CopiarAsync(vm.TextoCompleto()); e.Handled = true; return; }
        if (ctrl && e.Key == Key.C) { vm.Interrumpir(); e.Handled = true; return; }
        if (ctrl && (e.Key == Key.D || e.Key == Key.Z)) { vm.CerrarEntradaEstandar(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.L) { vm.LimpiarPantalla(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.V) { _ = PegarAsync(vm); e.Handled = true; return; }
        if (ctrl && e.Key == Key.U) { Aplicar(vm.BorrarHastaElInicio(_entrada.Text ?? "", _entrada.CaretIndex)); e.Handled = true; return; }
        if (ctrl && e.Key == Key.W) { Aplicar(vm.BorrarPalabraAtras(_entrada.Text ?? "", _entrada.CaretIndex)); e.Handled = true; return; }
        if (ctrl && e.Key == Key.A) { _entrada.CaretIndex = 0; e.Handled = true; return; }
        if (ctrl && e.Key == Key.E) { _entrada.CaretIndex = (_entrada.Text ?? "").Length; e.Handled = true; return; }

        if (e.Key == Key.Enter) { vm.ConfirmarLinea(); e.Handled = true; return; }
        if (e.Key == Key.Tab) { Aplicar(vm.Autocompletar(_entrada.Text ?? "", _entrada.CaretIndex)); e.Handled = true; return; }

        if (e.Key == Key.Up) { vm.RecorrerHistorial(-1); e.Handled = true; return; }
        if (e.Key == Key.Down) { vm.RecorrerHistorial(1); e.Handled = true; return; }

        if (e.Key == Key.PageUp) { _scroll.PageUp(); e.Handled = true; return; }
        if (e.Key == Key.PageDown) { _scroll.PageDown(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.Home) { _scroll.ScrollToHome(); e.Handled = true; return; }
        if (ctrl && e.Key == Key.End) { _scroll.ScrollToEnd(); e.Handled = true; return; }

    }

    void Aplicar((string Texto, int Caret) resultado)
    {
        _entrada.Text = resultado.Texto;
        _entrada.CaretIndex = resultado.Caret;
    }

    async Task PegarAsync(TerminalInstanceViewModel vm)
    {
        var portapapeles = TopLevel.GetTopLevel(this)?.Clipboard;
        if (portapapeles == null) return;
        var texto = await portapapeles.TryGetTextAsync();
        if (string.IsNullOrEmpty(texto)) return;

        string aplanado = TerminalInstanceViewModel.AplanarParaPegar(texto);
        int caret = _entrada.CaretIndex;
        string actual = _entrada.Text ?? "";
        _entrada.Text = actual.Insert(caret, aplanado);
        _entrada.CaretIndex = caret + aplanado.Length;
    }

    async Task CopiarAsync(string texto)
    {
        var portapapeles = TopLevel.GetTopLevel(this)?.Clipboard;
        if (portapapeles != null) await portapapeles.SetTextAsync(texto);
    }
}
