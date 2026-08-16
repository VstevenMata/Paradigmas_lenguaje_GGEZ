using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class PanelInspeccion
{
    // VARIABLES CONSTANTES
    const int AltoEncabezado = 22;
    const int AltoFila = 22;
    const int AnchoColumnaVariable = 90;
    const int AnchoPestana = 28;
    const float VelocidadAnimacion = 1400f;

    public void Update()
    {
        float objetivo = Oculto ? AnchoPestana : boundsHome.Width;
        float paso = VelocidadAnimacion * Raylib.GetFrameTime();
        anchoActual = MathF.Abs(objetivo - anchoActual) <= paso ? objetivo : anchoActual + MathF.Sign(objetivo - anchoActual) * paso;

         panel.Bounds = new Rectangle(boundsHome.X + boundsHome.Width - anchoActual, boundsHome.Y, anchoActual, boundsHome.Height);
        panel.Minimized = Oculto;

        panel.Update();
        if (panel.BotonExtraClickeado()) Oculto = !Oculto;
    }

    public void Draw()
    {
        panel.Draw(DibujarContenido);
    }

    void DibujarContenido(Rectangle area)
    {
        Raylib.DrawRectangleRec(area, Color.White);
        Win95Style.DrawBevel(area, sunken: true, thickness: 2);

        var encabezado = new Rectangle(area.X + 2, area.Y + 2, area.Width - 4, AltoEncabezado);
        Raylib.DrawRectangleRec(encabezado, Win95Style.Background);
        Win95Style.DrawBevel(encabezado, sunken: false, thickness: 1);
        Win95Style.DrawText("Variable", (int)encabezado.X + 6, (int)encabezado.Y + 3, 16, Color.Black);
        Win95Style.DrawText("Valor", (int)encabezado.X + AnchoColumnaVariable + 6, (int)encabezado.Y + 3, 16, Color.Black);

        float y = encabezado.Y + encabezado.Height;
        foreach (var (nombre, valor) in Variables)
        {
            Win95Style.DrawText(nombre, (int)area.X + 8, (int)y + 3, 16, Color.Black);
            Win95Style.DrawText(valor, (int)area.X + AnchoColumnaVariable + 6, (int)y + 3, 16, new Color(0, 0, 200, 255));
            y += AltoFila;
            Win95Style.DrawDivisorHorizontal((int)area.X + 2, (int)y, (int)area.Width - 4);
        }
    }

    public Rectangle Bounds
    {
        get => boundsHome;
        set => boundsHome = value;
    }

    public int AlturaSugerida => Panel.TitleBarHeight + 8 + AltoEncabezado + AltoFila * Variables.Length;

        public float BordeIzquierdoActual => boundsHome.X + boundsHome.Width - anchoActual;

    public bool Oculto;

    public (string Nombre, string Valor)[] Variables =
    {
        ("Jorge", "Maikel"),
        ("Jeremy", "Tiso"),
        ("Samuel", "Alex"),
        ("Steven", "El goat"),
        ("Pablo", "El goat x2"),
    };

    Rectangle boundsHome;
    float anchoActual;

    readonly Panel panel;

    public PanelInspeccion(Texture2D iconoPuertaAbierta, Texture2D iconoPuertaCerrada)
    {
        panel = new Panel(0, 0, 0, 0, "Inspeccion")
        {
            MostrarMinimizar = false,
            MostrarMaximizar = false,
            MostrarCerrar = false,
            MostrarBotonExtra = true,
            IconoBotonExtra = r =>
            {
                var icono = Oculto ? iconoPuertaCerrada : iconoPuertaAbierta;
                float escala = r.Width / icono.Width;
                Raylib.DrawTextureEx(icono, new Vector2(r.X, r.Y), 0f, escala, Color.White);
            }
        };
    }
}
