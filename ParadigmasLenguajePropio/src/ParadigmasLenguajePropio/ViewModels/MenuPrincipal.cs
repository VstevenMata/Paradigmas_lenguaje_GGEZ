using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

public partial class MenuPrincipal : ObservableObject
{
    public string Titulo { get; }
    public IReadOnlyList<ItemMenu> Items { get; }

    [ObservableProperty]
    private bool _esAbierto;

    public MenuPrincipal(string titulo, IReadOnlyList<ItemMenu> items)
    {
        Titulo = titulo;
        Items = items;
    }
}
