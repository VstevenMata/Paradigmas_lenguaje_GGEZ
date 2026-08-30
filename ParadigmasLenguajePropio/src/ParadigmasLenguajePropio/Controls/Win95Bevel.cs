using Avalonia;
using Avalonia.Controls;

namespace ParadigmasLenguajePropio.Controls;

// Reemplazo de Win95Style.DrawBevel: borde de 1-2px, claro arriba/izquierda y oscuro abajo/derecha
// (invertido cuando Sunken=true), el bisel clasico de los controles de Windows 95.
public class Win95Bevel : ContentControl
{
    public static readonly StyledProperty<bool> SunkenProperty =
        AvaloniaProperty.Register<Win95Bevel, bool>(nameof(Sunken));

    public static readonly StyledProperty<double> BevelThicknessProperty =
        AvaloniaProperty.Register<Win95Bevel, double>(nameof(BevelThickness), 2);

    // para items de lista (ej. filas de un submenu) que solo muestran el bisel al pasar el mouse:
    // en vez de dibujarlo siempre, se puede ocultar y mostrar segun un bool bindeado
    public static readonly StyledProperty<bool> TieneBiselProperty =
        AvaloniaProperty.Register<Win95Bevel, bool>(nameof(TieneBisel), true);

    public bool Sunken
    {
        get => GetValue(SunkenProperty);
        set => SetValue(SunkenProperty, value);
    }

    public double BevelThickness
    {
        get => GetValue(BevelThicknessProperty);
        set => SetValue(BevelThicknessProperty, value);
    }

    public bool TieneBisel
    {
        get => GetValue(TieneBiselProperty);
        set => SetValue(TieneBiselProperty, value);
    }

    static Win95Bevel()
    {
        SunkenProperty.Changed.AddClassHandler<Win95Bevel>((c, _) => c.PseudoClasses.Set(":sunken", c.Sunken));
    }
}
