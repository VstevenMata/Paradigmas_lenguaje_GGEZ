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

    AppTitleBarViewModel? ViewModel => DataContext as AppTitleBarViewModel;

    public AppTitleBarView()
    {
        InitializeComponent();

        _fondo = this.FindControl<Border>("PART_Fondo")!;
        this.FindControl<Button>("PART_Minimizar")!.Click += (_, _) =>
        {
            if (_window == null) return;
            // niri no tiene minimizar: se manda la ventana al workspace de abajo
            if (EnNiri) NiriAccion("move-window-to-workspace-down", "--focus", "false");
            else _window.WindowState = WindowState.Minimized;
        };
        this.FindControl<Button>("PART_Maximizar")!.Click += (_, _) =>
        {
            if (_window == null) return;
            // xwayland-satellite ignora el maximizar de X11, se le pide directo a niri
            if (EnNiri) NiriAccion("maximize-window-to-edges");
            else _window.WindowState = _window.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
        };
        this.FindControl<Button>("PART_Cerrar")!.Click += (_, _) => _window?.Close();

        var campo = this.FindControl<TextBox>("PART_CampoBusqueda")!;
        campo.KeyDown += CampoBusqueda_KeyDown;

        _fondo.PointerPressed += Fondo_PointerPressed;
    }

    static bool EnNiri => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable("NIRI_SOCKET"));

    static void NiriAccion(params string[] args)
    {
        try { System.Diagnostics.Process.Start("niri", ["msg", "action", .. args]); }
        catch (System.ComponentModel.Win32Exception) { }
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _window = TopLevel.GetTopLevel(this) as Window;
        if (_window == null) return;

        // Arranca con el color activo del XAML: aqui la ventana aun no se muestra e IsActive da false,
        // y hay gestores de ventanas que nunca mandan Activated, asi que quedaba gris para siempre
        _window.Activated += (_, _) => ActualizarFondoSegunFoco(true);
        _window.Deactivated += (_, _) => ActualizarFondoSegunFoco(false);
    }

    IDisposable? _fondoRecurso;

    // Enlazado al recurso (no copiado) para que tambien cambie al cambiar de tema
    void ActualizarFondoSegunFoco(bool activa)
    {
        _fondoRecurso?.Dispose();
        _fondoRecurso = _fondo.Bind(Border.BackgroundProperty,
            this.GetResourceObservable(activa ? "TemaTitleBarActivaBrush" : "TemaTitleBarInactivaBrush"));
    }

    // Sin decoracion nativa, BeginMoveDrag delega el arrastre al gestor de ventanas
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
