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

        // FileDialogService/Clipboard necesitan la Window/TopLevel, no existen hasta que la ventana se crea
        vm.Explorer.Archivos = new FileDialogService(this);
        vm.Editor.Archivos = new FileDialogService(this);
        vm.Clipboard = Clipboard;

        // si quedó un comando corriendo, se corta acá: si no, sobrevive a la ventana
        Closing += (_, _) => vm.Terminal.Cerrar();

        // la terminal puede crecer hasta la mitad de este Grid (editor+explorador+juego+terminal):
        // un tope fijo en pixeles se queda corto al maximizar y sobra en una ventana chica
        var gridContenido = this.FindControl<Grid>("PART_GridContenido")!;
        gridContenido.SizeChanged += (_, e) => vm.AlturaTerminalMaxima = Math.Max(160, e.NewSize.Height / 2);
    }

    void CerrarMenu_PointerPressed(object? sender, PointerPressedEventArgs e) => ViewModel?.MenuBar.CerrarTodos();
}
