using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class StatusBar
{
    // VARIABLES CONSTANTES
    public const int Height = 26;
    const int FontSize = 15;

    public void Draw()
    {
        Raylib.DrawRectangleRec(Bounds, Win95Style.Background);
        Win95Style.DrawDivisorHorizontal((int)Bounds.X, (int)Bounds.Y, (int)Bounds.Width);

        float x = Bounds.X + Bounds.Width - 2;
        for (int i = Segmentos.Length - 1; i >= 0; i--)
        {
            int ancho = Win95Style.MeasureText(Segmentos[i], FontSize) + 12;
            x -= ancho;
            var r = new Rectangle(x, Bounds.Y + 2, ancho, Bounds.Height - 4);
            Raylib.DrawRectangleRec(r, Win95Style.Background);
            Win95Style.DrawBevel(r, sunken: true, thickness: 1);
            Win95Style.DrawText(Segmentos[i], (int)r.X + 6, (int)r.Y + 4, FontSize, Color.Black);
            x -= 2;
        }

        var rMensaje = new Rectangle(Bounds.X + 2, Bounds.Y + 2, x - Bounds.X - 4, Bounds.Height - 4);
        Raylib.DrawRectangleRec(rMensaje, Win95Style.Background);
        Win95Style.DrawBevel(rMensaje, sunken: true, thickness: 1);
        Win95Style.DrawText(Mensaje, (int)rMensaje.X + 6, (int)rMensaje.Y + 4, FontSize, Color.Black);
    }

    public Rectangle Bounds;
    public string Mensaje = "If it's not fun, why do it?";
    public string[] Segmentos = { "Steven", "Maikel", "Tiso" };
}
