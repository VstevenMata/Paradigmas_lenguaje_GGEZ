using Avalonia;
using Avalonia.Controls;

namespace ParadigmasLenguajePropio.Controls;

// Bisel clasico de Windows 95: claro arriba/izquierda, oscuro abajo/derecha (invertido con Sunken)
public class Win95Bevel : ContentControl
{
    public static readonly StyledProperty<bool> SunkenProperty =
        AvaloniaProperty.Register<Win95Bevel, bool>(nameof(Sunken));

    public static readonly StyledProperty<double> BevelThicknessProperty =
        AvaloniaProperty.Register<Win95Bevel, double>(nameof(BevelThickness), 2);

    // Permite mostrar el bisel solo al pasar el mouse (filas de submenu)
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
