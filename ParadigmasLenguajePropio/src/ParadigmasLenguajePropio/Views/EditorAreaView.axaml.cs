using Avalonia.Controls;
using Avalonia.Media;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class EditorAreaView : UserControl
{
    EditorAreaViewModel? ViewModel => DataContext as EditorAreaViewModel;

    ContentControl _host = null!;

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
            };
            Reconstruir(vm);
        };
    }

    // splits pasan pocas veces por sesion (el usuario arrastra una pestaña de vez en cuando):
    // reconstruir el arbol de controles entero cada vez que Raiz cambia es simple y sobra de
    // rapido, no hace falta un diff incremental
    void Reconstruir(EditorAreaViewModel vm) => _host.Content = Construir(vm.Raiz, vm);

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
