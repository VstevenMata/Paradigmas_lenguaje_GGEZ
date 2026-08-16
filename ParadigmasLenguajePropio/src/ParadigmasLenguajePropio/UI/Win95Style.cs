using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public static class Win95Style
{
    // VARIABLES CONSTANTES
    public static readonly Color Background = new(192, 192, 192, 255);
    public static readonly Color ScrollTrack = new(160, 160, 160, 255);
    public static readonly Color TitleBar = new(0, 0, 128, 255);
    public static readonly Color BevelLight = Color.White;
    public static readonly Color BevelDark = new(64, 64, 64, 255);
    public static readonly Color TooltipFondo = new(255, 255, 225, 255);
    public static readonly Color TextoDeshabilitado = new(128, 128, 128, 255);

    public static void CargarFuente(string ruta, int tamanoBase = 16)
    {
        
        Fuente = Raylib.LoadFontEx(ruta, tamanoBase, null, 0);
          Raylib.SetTextureFilter(Fuente.Texture, TextureFilter.Bilinear);
    }

    public static void DrawText(string texto, int posX, int posY, int fontSize, Color color)
    {
        Raylib.DrawTextEx(Fuente, texto, new System.Numerics.Vector2(posX, posY), fontSize, 1, color);
    }

    public static int MeasureText(string texto, int fontSize)
    {
        return (int)Raylib.MeasureTextEx(Fuente, texto, fontSize, 1).X;
    }

    public static void DrawBevel(Rectangle bounds, bool sunken, int thickness = 2, bool bordeInferior = true)
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
            if (bordeInferior)
                Raylib.DrawLine((int)x, (int)(y + h), (int)(x + w), (int)(y + h), bottom);
        }
    }

    public static void DrawDivisorHorizontal(int x, int y, int width, int thickness = 1)
    {
        for (int i = 0; i < thickness; i++)
        {
            int lineY = y + i * 2;
            Raylib.DrawLine(x, lineY, x + width, lineY, BevelDark);
            Raylib.DrawLine(x, lineY + 1, x + width, lineY + 1, BevelLight);
        }
    }

    public static void DrawDivisorVertical(int x, int y, int height, int thickness = 1)
    {
        for (int i = 0; i < thickness; i++)
        {
            int lineX = x + i * 2;
            Raylib.DrawLine(lineX, y, lineX, y + height, BevelDark);
            Raylib.DrawLine(lineX + 1, y, lineX + 1, y + height, BevelLight);
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

    // globo amarillo clásico de Win95: fondo pálido, borde negro fino, sin bevel
    public static void DrawTooltip(string texto, int x, int y)
    {
        int ancho = MeasureText(texto, 14) + 8;
        var r = new Rectangle(x, y, ancho, 18);
        Raylib.DrawRectangleRec(r, TooltipFondo);
        Raylib.DrawRectangleLinesEx(r, 1, Color.Black);
        DrawText(texto, x + 4, y + 2, 14, Color.Black);
    }

    // texto "grabado": una copia clara desplazada 1px y la gris encima, como los controles inactivos de Win95
    public static void DrawTextDeshabilitado(string texto, int x, int y, int fontSize)
    {
        DrawText(texto, x + 1, y + 1, fontSize, BevelLight);
        DrawText(texto, x, y, fontSize, TextoDeshabilitado);
    }

    public static Font Fuente;
}
