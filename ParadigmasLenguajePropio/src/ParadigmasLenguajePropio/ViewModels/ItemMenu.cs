using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

// EsResaltado a mano desde el code-behind: mas confiable que :pointerover con combinador de descendiente
public partial class ItemMenu : ObservableObject
{
    public string Texto { get; }
    public bool EsSeparador { get; }

    [ObservableProperty]
    private bool _esResaltado;

    public ItemMenu(string texto, bool esSeparador = false)
    {
        Texto = texto;
        EsSeparador = esSeparador;
    }

    public static readonly ItemMenu Separador = new("-", esSeparador: true);
}
