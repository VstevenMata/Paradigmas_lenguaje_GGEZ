using Avalonia.Controls;
using Avalonia.Input;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class MenuBarView : UserControl
{
    MenuBarViewModel? ViewModel => DataContext as MenuBarViewModel;

    public MenuBarView()
    {
        InitializeComponent();
    }

    void Encabezado_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: MenuPrincipal menu } && ViewModel is { } vm)
            vm.AbrirOCerrarCommand.Execute(menu);
    }

    // con un menu ya abierto, pasar el mouse sobre otro encabezado lo abre a ese
    void Encabezado_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: MenuPrincipal menu } && ViewModel is { } vm)
            vm.AbrirSiOtroYaEstaAbierto(menu);
    }

    void Item_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: ItemMenu item } && !item.EsSeparador && ViewModel is { } vm)
            vm.ElegirCommand.Execute(item.Texto);
    }

    // el hover de las filas del submenu se maneja a mano (no con Style ":pointerover") por lo mismo
    // que EsActiva/EsAbierto en otros lados: mas confiable que un selector con combinador de descendiente
    void Item_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: ItemMenu item }) item.EsResaltado = true;
    }

    void Item_PointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: ItemMenu item }) item.EsResaltado = false;
    }
}
