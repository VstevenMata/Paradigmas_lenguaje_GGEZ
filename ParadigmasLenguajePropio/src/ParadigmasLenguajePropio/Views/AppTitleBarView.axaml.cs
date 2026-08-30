using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class AppTitleBarView : UserControl
{
    Window? _window;
    Border _fondo = null!;

    // el DataContext lo asigna quien compone esta vista (MainWindow, vía binding a MainWindowViewModel.TitleBar)
    AppTitleBarViewModel? ViewModel => DataContext as AppTitleBarViewModel;

    public AppTitleBarView()
    {
        InitializeComponent();

        _fondo = this.FindControl<Border>("PART_Fondo")!;
        this.FindControl<Button>("PART_Minimizar")!.Click += (_, _) => { if (_window != null) _window.WindowState = WindowState.Minimized; };
        this.FindControl<Button>("PART_Maximizar")!.Click += (_, _) =>
        {
            if (_window == null) return;
            _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        };
        this.FindControl<Button>("PART_Cerrar")!.Click += (_, _) => _window?.Close();

        var campo = this.FindControl<TextBox>("PART_CampoBusqueda")!;
        campo.KeyDown += CampoBusqueda_KeyDown;

        _fondo.PointerPressed += Fondo_PointerPressed;
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window == null) return;

        _window.Activated += (_, _) => ActualizarFondoSegunFoco();
        _window.Deactivated += (_, _) => ActualizarFondoSegunFoco();
        ActualizarFondoSegunFoco();
    }

    void ActualizarFondoSegunFoco()
    {
        bool activa = _window?.IsActive ?? true;
        _fondo.Background = (Avalonia.Media.IBrush?)this.FindResource(
            activa ? "Win95TitleBarActivaBrush" : "Win95TitleBarInactivaBrush");
    }

    // sin decoracion nativa no hay barra que el SO arrastre solo: BeginMoveDrag delega el arrastre
    // al gestor de ventanas. Los controles hijos (botones, buscador) marcan el evento manejado,
    // asi que este handler solo se dispara al clickear el fondo vacio de la franja.
    void Fondo_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_window != null && e.GetCurrentPoint(_fondo).Properties.IsLeftButtonPressed)
            _window.BeginMoveDrag(e);
    }

    void CampoBusqueda_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        switch (e.Key)
        {
            case Key.Down: vm.MoverSeleccion(1); e.Handled = true; break;
            case Key.Up: vm.MoverSeleccion(-1); e.Handled = true; break;
            case Key.Enter: vm.AbrirSeleccionado(); e.Handled = true; break;
            case Key.Escape: vm.CerrarResultados(); e.Handled = true; break;
        }
    }

    void ResultadoFila_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is StyledElement { DataContext: ResultadoBusqueda resultado })
            ViewModel?.AbrirResultadoEn(resultado);
    }
}
