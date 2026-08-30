using Avalonia.Controls;
using Avalonia.Interactivity;

namespace ParadigmasLenguajePropio.Views;

public partial class PanelJuegoView : UserControl
{
    public PanelJuegoView()
    {
        InitializeComponent();
    }

    void Ok_Click(object? sender, RoutedEventArgs e) => Console.WriteLine("Runner: OK");
    void Cancel_Click(object? sender, RoutedEventArgs e) => Console.WriteLine("Runner: Cancel");
}
