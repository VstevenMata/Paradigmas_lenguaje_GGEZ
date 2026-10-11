using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

// EsResaltado a mano desde el code-behind: mas confiable que :pointerover con combinador de descendiente
public partial class ItemMenu : ObservableObject
{
    public string Texto { get; }
    public bool EsSeparador { get; }
    public string Atajo { get; }
    public KeyGesture? Gesto { get; }

    [ObservableProperty]
    private bool _esResaltado;

    // gesto solo hace falta cuando el texto visible no es parseable (ej. "Ctrl++" -> "Ctrl+OemPlus")
    public ItemMenu(string texto, string atajo = "", string? gesto = null, bool esSeparador = false)
    {
        Texto = texto;
        EsSeparador = esSeparador;
        Atajo = atajo;
        gesto ??= atajo;
        if (gesto != "") Gesto = KeyGesture.Parse(gesto);
    }

    public static readonly ItemMenu Separador = new("-", esSeparador: true);
}
