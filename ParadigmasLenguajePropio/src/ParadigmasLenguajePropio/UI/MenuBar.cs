using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class MenuBar
{
    public const int Height = 20;
    public string[] Items = { "Archivo", "Editar", "Ejecutar", "Ayuda" };

    public Rectangle Bounds;

    public void Draw()
    {
        Raylib.DrawRectangleRec(Bounds, Win95Style.Background);

        int x = (int)Bounds.X + 6;
        foreach (var item in Items)
        {
            Raylib.DrawText(item, x, (int)Bounds.Y + 4, 12, Color.Black);
            x += Raylib.MeasureText(item, 12) + 18;
        }
    }
}
