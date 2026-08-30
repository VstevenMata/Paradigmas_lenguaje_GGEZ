using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

public partial class StatusBarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _mensaje = "If it's not fun, why do it?";

    [ObservableProperty]
    private string _posicionCursor = "";

    public ObservableCollection<string> Segmentos { get; } = new(["Steven", "Maikel", "Tiso"]);
}
