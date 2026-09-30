using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Media;
using ParadigmasLenguajePropio.Controls;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class ExplorerView : UserControl
{
    ExplorerViewModel? ViewModel => DataContext as ExplorerViewModel;

    ExplorerTreeView _arbol = null!;
    Popup _menuContextual = null!;
    StackPanel _itemsMenuContextual = null!;
    Border _catchAll = null!;
    Control _area = null!;

    public ExplorerView()
    {
        InitializeComponent();

        _arbol = this.FindControl<ExplorerTreeView>("PART_Arbol")!;
        _menuContextual = this.FindControl<Popup>("PART_MenuContextual")!;
        _itemsMenuContextual = this.FindControl<StackPanel>("PART_ItemsMenuContextual")!;
        _catchAll = this.FindControl<Border>("PART_CatchAll")!;
        _area = this.FindControl<Control>("PART_Area")!;

        // Sin PlacementTarget el popup con Placement="Pointer" cubria toda la pantalla
        _menuContextual.PlacementTarget = _area;
        _menuContextual.Placement = PlacementMode.Pointer;

        _arbol.ArchivoAbierto += nodo => ViewModel?.AbrirSiEsArchivo(nodo);
        _arbol.MenuContextualSolicitado += (nodo, punto) => AbrirMenuContextual(nodo, punto);
    }

    void Vacio_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsRightButtonPressed) return;
        AbrirMenuVacio();
    }

    void AbrirMenuContextual(NodoArchivos nodo, Point _)
    {
        if (ViewModel is not { } vm) return;

        _itemsMenuContextual.Children.Clear();
        _itemsMenuContextual.Children.Add(CrearItem("Nuevo archivo", () => vm.NuevoArchivoCommand.Execute(nodo)));
        _itemsMenuContextual.Children.Add(CrearItem("Nueva carpeta", () => vm.NuevaCarpetaCommand.Execute(nodo)));
        if (nodo.EsRaizDeProyecto)
            _itemsMenuContextual.Children.Add(CrearItem("Cerrar carpeta", () => vm.CerrarCarpetaCommand.Execute(null)));
        else
            _itemsMenuContextual.Children.Add(CrearItem("Renombrar", () => vm.RenombrarCommand.Execute(nodo)));
        _itemsMenuContextual.Children.Add(CrearItem("Eliminar", () => vm.EliminarCommand.Execute(nodo)));

        AbrirPopup();
    }

    void AbrirMenuVacio()
    {
        if (ViewModel is not { } vm) return;

        _itemsMenuContextual.Children.Clear();
        _itemsMenuContextual.Children.Add(CrearItem("Crear carpeta", () => vm.CrearCarpetaCommand.Execute(null)));
        _itemsMenuContextual.Children.Add(CrearItem("Abrir carpeta", () => vm.AbrirCarpetaDialogoCommand.Execute(null)));

        AbrirPopup();
    }

    void AbrirPopup()
    {
        _catchAll.IsVisible = true;
        _menuContextual.IsOpen = true;
    }

    void CerrarMenu_PointerPressed(object? sender, PointerPressedEventArgs e) => CerrarMenu();

    void CerrarMenu()
    {
        _menuContextual.IsOpen = false;
        _catchAll.IsVisible = false;
    }

    Border CrearItem(string texto, Action accion)
    {
        var bevel = new Win95Bevel
        {
            Sunken = true,
            BevelThickness = 1,
            TieneBisel = false,
            Padding = new Thickness(17, 3),
            Content = new TextBlock { Text = texto, FontSize = 14, Foreground = Brushes.Black },
        };

        var borde = new Border { Background = Brushes.Transparent, Child = bevel };
        borde.PointerEntered += (_, _) => bevel.TieneBisel = true;
        borde.PointerExited += (_, _) => bevel.TieneBisel = false;
        borde.PointerPressed += (_, _) => { accion(); CerrarMenu(); };
        return borde;
    }
}
