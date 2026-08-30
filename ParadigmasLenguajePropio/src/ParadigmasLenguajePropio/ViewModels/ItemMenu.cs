using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

// EsResaltado se maneja a mano desde el code-behind (PointerEntered/PointerExited) en vez de con
// un Style Selector":pointerover" con combinador de descendiente: es mas confiable, el mismo
// patron que EsActiva/EsAbierto en otros lados de la app.
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
