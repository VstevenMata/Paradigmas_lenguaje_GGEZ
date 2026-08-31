using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class EditorAreaView : UserControl
{
    EditorAreaViewModel? ViewModel => DataContext as EditorAreaViewModel;

    ContentControl _host = null!;

    // que vista corresponde a cada panel, para poder enfocar la correcta sin recorrer el
    // visual tree - se reconstruye entera cada vez que Raiz cambia, junto con las vistas
    readonly Dictionary<EditorViewModel, EditorView> _vistasPorPanel = new();

    public EditorAreaView()
    {
        InitializeComponent();
        _host = this.FindControl<ContentControl>("PART_Host")!;

        DataContextChanged += (_, _) =>
        {
            if (ViewModel is not { } vm) return;
            vm.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(EditorAreaViewModel.Raiz)) Reconstruir(vm);
                else if (e.PropertyName == nameof(EditorAreaViewModel.PanelEnfocado)) EnfocarPanelActual(vm);
            };
            Reconstruir(vm);
        };
    }

    // splits pasan pocas veces por sesion (el usuario arrastra una pestaña de vez en cuando):
    // reconstruir el arbol de controles entero cada vez que Raiz cambia es simple y sobra de
    // rapido, no hace falta un diff incremental
    void Reconstruir(EditorAreaViewModel vm)
    {
        _vistasPorPanel.Clear();
        _host.Content = Construir(vm.Raiz, vm);
        EnfocarPanelActual(vm);
    }

    // se llama tanto al reconstruir el arbol como cada vez que PanelEnfocado cambia SOLO (sin
    // que cambie Raiz, ej: mover una pestaña Centro a otro panel ya existente). El Post busca
    // la vista recien cuando efectivamente corre, no antes - asi da lo mismo el orden entre
    // "cambio Raiz" y "cambio PanelEnfocado" (varia segun la operacion) porque para cuando el
    // callback se ejecuta la cascada sincronica ya termino del todo y el estado es el final.
    // Sin esto, tras partir/podar un panel el foco de teclado quedaba huerfano en una vista ya
    // destruida y no se podia escribir en ningun lado hasta clickear a mano.
    void EnfocarPanelActual(EditorAreaViewModel vm)
    {
        Dispatcher.UIThread.Post(() =>
        {
            if (_vistasPorPanel.TryGetValue(vm.PanelEnfocado, out var vista)) vista.EnfocarTexto();
        });
    }

    Control Construir(NodoPanel nodo, EditorAreaViewModel vm)
    {
        if (nodo.EsHoja)
        {
            var panel = nodo.Panel!;
            var vista = new EditorView
            {
                DataContext = panel,
                PuedeDividir = vm.ContarHojas() < EditorAreaViewModel.MaxPaneles,
            };
            vista.GotFocus += (_, _) => vm.EnfocarPanel(panel);
            vista.TabSoltado += (tab, origen, destino, zona) => vm.MoverTab(tab, origen, destino, zona);
            _vistasPorPanel[panel] = vista;
            return vista;
        }

        bool apilado = nodo.Orientacion == OrientacionSplit.Apilado;
        var grid = new Grid();

        if (apilado)
        {
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            grid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
        }
        else
        {
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Auto));
            grid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
        }

        var hijoA = Construir(nodo.A!, vm);
        var hijoB = Construir(nodo.B!, vm);
        var splitter = new GridSplitter
        {
            Background = Brushes.Transparent,
            ResizeDirection = apilado ? GridResizeDirection.Rows : GridResizeDirection.Columns,
        };
        if (apilado) splitter.Height = 6; else splitter.Width = 6;

        if (apilado)
        {
            Grid.SetRow(hijoA, 0);
            Grid.SetRow(splitter, 1);
            Grid.SetRow(hijoB, 2);
        }
        else
        {
            Grid.SetColumn(hijoA, 0);
            Grid.SetColumn(splitter, 1);
            Grid.SetColumn(hijoB, 2);
        }

        grid.Children.Add(hijoA);
        grid.Children.Add(splitter);
        grid.Children.Add(hijoB);
        return grid;
    }
}
