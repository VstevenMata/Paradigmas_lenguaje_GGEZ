using Avalonia.Controls;
using Avalonia.Input;
using ParadigmasLenguajePropio.Services;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class MainWindow : Window
{
    MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    public MainWindow()
    {
        InitializeComponent();

        var vm = new MainWindowViewModel();
        DataContext = vm;

        // FileDialogService y Clipboard necesitan la Window ya creada
        vm.Explorer.Archivos = new FileDialogService(this);
        vm.Editor.Archivos = new FileDialogService(this);
        vm.Clipboard = Clipboard;

        // Si quedo un comando corriendo sobreviviria a la ventana
        Closing += (_, _) => vm.Terminal.Cerrar();
        Closing += (_, _) => vm.GuardarSesion();

        var gridContenido = this.FindControl<Grid>("PART_GridContenido")!;
        gridContenido.SizeChanged += (_, e) => vm.AlturaTerminalMaxima = Math.Max(160, e.NewSize.Height / 2);

        KeyDown += (_, e) =>
        {
            if (!e.Handled && !vm.Dialogs.IsVisible && vm.MenuBar.EjecutarAtajo(e)) e.Handled = true;
        };
    }

    void CerrarMenu_PointerPressed(object? sender, PointerPressedEventArgs e) => ViewModel?.MenuBar.CerrarTodos();

    // Cada grip trae en Tag el WindowEdge como texto, para no tener 8 metodos identicos
    void BordeResize_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { Tag: string ladoTexto } && Enum.TryParse<WindowEdge>(ladoTexto, out var lado)
            && e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            BeginResizeDrag(lado, e);
    }
}
