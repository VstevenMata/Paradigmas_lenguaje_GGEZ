using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

public partial class StatusBarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _posicionCursor = "";

    public ObservableCollection<string> Segmentos { get; } = new(["Steven", "Maikel", "Tiso"]);
}
