using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public static class Win95Style
{
    public static readonly Color Background = new(192, 192, 192, 255);
    public static readonly Color TitleBar = new(0, 0, 128, 255);
    public static readonly Color BevelLight = Color.White;
    public static readonly Color BevelDark = new(64, 64, 64, 255);

    public static void DrawBevel(Rectangle bounds, bool sunken, int thickness = 2)
    {
        Color top = sunken ? BevelDark : BevelLight;
        Color bottom = sunken ? BevelLight : BevelDark;

        for (int i = 0; i < thickness; i++)
        {
            float x = bounds.X + i;
            float y = bounds.Y + i;
            float w = bounds.Width - i * 2;
            float h = bounds.Height - i * 2;

            Raylib.DrawLine((int)x, (int)y, (int)(x + w), (int)y, top);
            Raylib.DrawLine((int)x, (int)y, (int)x, (int)(y + h), top);
            Raylib.DrawLine((int)(x + w), (int)y, (int)(x + w), (int)(y + h), bottom);
            Raylib.DrawLine((int)x, (int)(y + h), (int)(x + w), (int)(y + h), bottom);
        }
    }

    public static void DrawTitleButtonBase(Rectangle r)
    {
        bool hover = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r);
        Raylib.DrawRectangleRec(r, hover ? Color.LightGray : Background);
        DrawBevel(r, sunken: hover, thickness: 1);
    }

    public static void DrawMinimizeIcon(Rectangle r)
    {
        DrawTitleButtonBase(r);
        Raylib.DrawLine((int)r.X + 3, (int)(r.Y + r.Height - 4), (int)(r.X + r.Width - 3), (int)(r.Y + r.Height - 4), Color.Black);
    }

    public static void DrawMaximizeIcon(Rectangle r)
    {
        DrawTitleButtonBase(r);
        var square = new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
        Raylib.DrawRectangleLinesEx(square, 1, Color.Black);
    }

    public static void DrawCloseIcon(Rectangle r)
    {
        DrawTitleButtonBase(r);
        Raylib.DrawLine((int)r.X + 3, (int)r.Y + 3, (int)(r.X + r.Width - 3), (int)(r.Y + r.Height - 3), Color.Black);
        Raylib.DrawLine((int)(r.X + r.Width - 3), (int)r.Y + 3, (int)r.X + 3, (int)(r.Y + r.Height - 3), Color.Black);
    }
}
