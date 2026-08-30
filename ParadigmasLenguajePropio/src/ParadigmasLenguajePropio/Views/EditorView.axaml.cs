using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Threading;
using ParadigmasLenguajePropio.Controls;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class EditorView : UserControl
{
    EditorViewModel? ViewModel => DataContext as EditorViewModel;

    ScrollViewer _scrollVertical = null!;
    ScrollViewer _scrollHorizontal = null!;
    EditorTextArea _textArea = null!;
    TextBox _campoBuscar = null!;
    TextBox _campoIrALinea = null!;
    EditorTabViewModel? _tabConScrollReiniciado;

    public EditorView()
    {
        InitializeComponent();

        _scrollVertical = this.FindControl<ScrollViewer>("PART_ScrollVertical")!;
        _scrollHorizontal = this.FindControl<ScrollViewer>("PART_ScrollHorizontal")!;
        _textArea = this.FindControl<EditorTextArea>("PART_TextArea")!;
        _campoBuscar = this.FindControl<TextBox>("PART_CampoBuscar")!;
        _campoIrALinea = this.FindControl<TextBox>("PART_CampoIrALinea")!;

        _textArea.CursorMovido += AsegurarCursorVisible;

        DataContextChanged += (_, _) =>
        {
            if (ViewModel is not { } vm) return;
            vm.PropertyChanged += Vm_PropertyChanged;
        };
    }

    void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } vm) return;

        if (e.PropertyName == nameof(EditorViewModel.TabActiva) && vm.TabActiva != _tabConScrollReiniciado)
        {
            _tabConScrollReiniciado = vm.TabActiva;
            // cada pestaña arranca mostrando su propio inicio, como CambiarArchivoActivo en el original
            Dispatcher.UIThread.Post(() =>
            {
                _scrollVertical.Offset = new Vector(0, 0);
                _scrollHorizontal.Offset = new Vector(0, 0);
                _textArea.Focus();
            });
        }
        else if (e.PropertyName == nameof(EditorViewModel.BuscarVisible) && vm.BuscarVisible)
        {
            Dispatcher.UIThread.Post(() => { _campoBuscar.Focus(); _campoBuscar.SelectAll(); });
        }
        else if (e.PropertyName == nameof(EditorViewModel.IrALineaVisible) && vm.IrALineaVisible)
        {
            Dispatcher.UIThread.Post(() => { _campoIrALinea.Focus(); _campoIrALinea.SelectAll(); });
        }
    }

    // reemplaza AsegurarCursorVisible: en vez de recalcular scroll.Valor a mano, nudgea el
    // Offset de los ScrollViewer nativos para que el rect del cursor quede dentro del viewport
    void AsegurarCursorVisible()
    {
        var rectCursor = _textArea.ObtenerRectCursor();

        var vOffset = _scrollVertical.Offset;
        var vViewport = _scrollVertical.Viewport;
        double nuevoY = vOffset.Y;
        if (rectCursor.Top < vOffset.Y) nuevoY = rectCursor.Top;
        else if (rectCursor.Bottom > vOffset.Y + vViewport.Height) nuevoY = rectCursor.Bottom - vViewport.Height;
        _scrollVertical.Offset = new Vector(vOffset.X, Math.Max(0, nuevoY));

        var hOffset = _scrollHorizontal.Offset;
        var hViewport = _scrollHorizontal.Viewport;
        double nuevoX = hOffset.X;
        if (rectCursor.Left < hOffset.X) nuevoX = rectCursor.Left;
        else if (rectCursor.Right > hOffset.X + hViewport.Width - 20) nuevoX = rectCursor.Right - hViewport.Width + 20;
        _scrollHorizontal.Offset = new Vector(Math.Max(0, nuevoX), hOffset.Y);
    }

    void Tab_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: EditorTabViewModel tab } && ViewModel is { } vm)
            vm.CambiarArchivoActivo(tab);
    }

    void Cerrar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: EditorTabViewModel tab } && ViewModel is { } vm)
            _ = vm.CerrarArchivoAsync(tab);
        e.Handled = true; // no dejar que el click tambien seleccione la pestaña
    }

    void CampoBuscar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Key == Key.Enter) { vm.BuscarSiguiente(); e.Handled = true; }
        else if (e.Key == Key.Escape) { vm.BuscarVisible = false; e.Handled = true; }
    }

    void BuscarAnterior_Click(object? sender, RoutedEventArgs e) => ViewModel?.BuscarAnterior();
    void BuscarSiguiente_Click(object? sender, RoutedEventArgs e) => ViewModel?.BuscarSiguiente();
    void CerrarBusqueda_Click(object? sender, RoutedEventArgs e) { if (ViewModel is { } vm) vm.BuscarVisible = false; }
    void Reemplazar_Click(object? sender, RoutedEventArgs e) => ViewModel?.ReemplazarUno();
    void ReemplazarTodo_Click(object? sender, RoutedEventArgs e) => ViewModel?.ReemplazarTodo();

    void CampoIrALinea_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Key == Key.Enter) { vm.ConfirmarIrALinea(); e.Handled = true; }
        else if (e.Key == Key.Escape) { vm.IrALineaVisible = false; e.Handled = true; }
    }

    void CerrarIrALinea_Click(object? sender, RoutedEventArgs e) { if (ViewModel is { } vm) vm.IrALineaVisible = false; }

    // atajos de teclado esperables (Ctrl+F/Ctrl+H/Ctrl+G) que el original no tenia (solo menu/toolbar);
    // se agregan aca a nivel del EditorView entero porque el foco normalmente lo tiene EditorTextArea
    void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        if (e.Key == Key.F) { vm.BuscarVisible = true; vm.MostrarReemplazo = false; e.Handled = true; }
        else if (e.Key == Key.H) { vm.BuscarVisible = true; vm.MostrarReemplazo = true; e.Handled = true; }
        else if (e.Key == Key.G) { vm.IrALineaVisible = true; e.Handled = true; }
    }
}
