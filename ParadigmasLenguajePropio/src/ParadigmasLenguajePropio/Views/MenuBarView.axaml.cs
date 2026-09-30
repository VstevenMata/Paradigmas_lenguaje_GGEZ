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

    // Hover a mano: mas confiable que un Style :pointerover con combinador de descendiente
    void Item_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: ItemMenu item }) item.EsResaltado = true;
    }

    void Item_PointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: ItemMenu item }) item.EsResaltado = false;
    }
}
