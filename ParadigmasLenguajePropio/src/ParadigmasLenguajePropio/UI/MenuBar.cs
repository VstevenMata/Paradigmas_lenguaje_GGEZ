using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class MenuBar
{
    // VARIABLES CONSTANTES
    public const int Height = 24;

    public void Draw()
    {
        Raylib.DrawRectangleRec(Bounds, Win95Style.Background);

        int x = (int)Bounds.X + 6;
        foreach (var item in Items)
        {
            Win95Style.DrawText(item, x, (int)Bounds.Y + 4, 16, Color.Black);
            x += Win95Style.MeasureText(item, 16) + 18;
        }
    }

    public string[] Items = { "Archivo", "Editar", "Ejecutar", "Ayuda" };

    public Rectangle Bounds;
}
