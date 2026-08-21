using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public static class Win95Style
{
    public static readonly Color Background = new(192, 192, 192, 255);
    public static readonly Color ScrollTrack = new(160, 160, 160, 255);
    public static readonly Color TitleBar = new(0, 0, 128, 255);
    public static readonly Color TitleBarActivaInicio = new(0, 0, 128, 255);
    public static readonly Color TitleBarActivaFin = new(0, 174, 239, 255);
    // ventana sin foco: mismo degradado pero en gris
    public static readonly Color TitleBarInactivaInicio = new(128, 128, 128, 255);
    public static readonly Color TitleBarInactivaFin = new(192, 192, 192, 255);
    public static readonly Color BevelLight = Color.White;
    public static readonly Color BevelDark = new(64, 64, 64, 255);
    public static readonly Color TooltipFondo = new(255, 255, 225, 255);
    public static readonly Color TextoDeshabilitado = new(128, 128, 128, 255);

    public static void CargarFuente(string ruta, int tamanoBase = 16)
    {
        // con codepoints=null raylib solo carga ASCII (0x20-0x7E): sin esto, ñ/Ñ y tildes no tienen glifo
        var codepoints = new List<int>();
        for (int c = 0x20; c <= 0x7E; c++) codepoints.Add(c);
        codepoints.AddRange(new[] { 0xA1, 0xBF, 0xC1, 0xC9, 0xCD, 0xD1, 0xD3, 0xDA, 0xDC, 0xE1, 0xE9, 0xED, 0xF1, 0xF3, 0xFA, 0xFC });

        Fuente = Raylib.LoadFontEx(ruta, tamanoBase, codepoints.ToArray(), codepoints.Count);
        Raylib.SetTextureFilter(Fuente.Texture, TextureFilter.Bilinear);
    }

    // al dibujar letra por letra (necesario para reemplazar glifos faltantes) hay que sumar este
    // espaciado a mano, porque Raylib solo lo aplica automáticamente entre letras de un string completo
    const int Espaciado = 1;

    public static void DrawText(string texto, int posX, int posY, int fontSize, Color color)
    {
        if (!TieneGlifoFaltante(texto))
        {
            Raylib.DrawTextEx(Fuente, texto, new System.Numerics.Vector2(posX, posY), fontSize, Espaciado, color);
            return;
        }

        float x = posX;
        for (int i = 0; i < texto.Length; i++)
        {
            char c = texto[i];
            float anchoLetra;

            if (EsGlifoFaltante(c))
            {
                DrawGlifoFaltante(c, x, posY, fontSize, color);
                anchoLetra = AnchoGlifoFaltante(c, fontSize);
            }
            else
            {
                string letra = c.ToString();
                Raylib.DrawTextEx(Fuente, letra, new System.Numerics.Vector2(x, posY), fontSize, Espaciado, color);
                anchoLetra = Raylib.MeasureTextEx(Fuente, letra, fontSize, Espaciado).X;
            }

            x += anchoLetra + (i < texto.Length - 1 ? Espaciado : 0);
        }
    }

    public static int MeasureText(string texto, int fontSize)
    {
        if (!TieneGlifoFaltante(texto))
            return (int)Raylib.MeasureTextEx(Fuente, texto, fontSize, Espaciado).X;

        float total = 0;
        for (int i = 0; i < texto.Length; i++)
        {
            char c = texto[i];
            total += EsGlifoFaltante(c) ? AnchoGlifoFaltante(c, fontSize) : Raylib.MeasureTextEx(Fuente, c.ToString(), fontSize, Espaciado).X;
            if (i < texto.Length - 1) total += Espaciado;
        }
        return (int)total;
    }

    // el único glifo que realmente falta en la fuente es "<"/">"; se reemplaza por el ícono de puerta
    static readonly char[] GlifosFaltantes = { '<', '>' };
    static bool EsGlifoFaltante(char c) => Array.IndexOf(GlifosFaltantes, c) >= 0;
    static bool TieneGlifoFaltante(string texto) => texto.IndexOfAny(GlifosFaltantes) >= 0;

    static float AnchoGlifoFaltante(char c, int fontSize) => fontSize;

    static void DrawGlifoFaltante(char c, float x, float y, int fontSize, Color color)
    {
        var textura = c == '<' ? GlifoMenor : GlifoMayor;
        var origen = new Rectangle(0, 0, textura.Width, textura.Height);
        var destino = new Rectangle(x, y, fontSize, fontSize);
        Raylib.DrawTexturePro(textura, origen, destino, System.Numerics.Vector2.Zero, 0f, Color.White);
    }

    public static Texture2D GlifoMenor;
    public static Texture2D GlifoMayor;

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

    // trama a cuadros de 1px, el "dither" clásico de Win95/98 para resaltar el elemento activo
    public static void DrawDither(Rectangle r, Color claro, Color oscuro)
    {
        int x0 = (int)r.X, y0 = (int)r.Y, x1 = (int)(r.X + r.Width), y1 = (int)(r.Y + r.Height);
        for (int y = y0; y < y1; y++)
            for (int x = x0; x < x1; x++)
                Raylib.DrawPixel(x, y, (x + y) % 2 == 0 ? claro : oscuro);
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

    // flechita hacia abajo, para desplegables (ej: el menú junto al buscador de la barra de título)
    public static void DrawChevronAbajoIcon(Rectangle r)
    {
        DrawTitleButtonBase(r);
        float cx = r.X + r.Width / 2f;
        float cy = r.Y + r.Height / 2f;
        Raylib.DrawTriangle(new(cx - 3, cy - 2), new(cx + 3, cy - 2), new(cx, cy + 2), Color.Black);
    }

    // rectángulo partido a la mitad, ej: alternar el diseño/layout de paneles
    public static void DrawDisenoIcon(Rectangle r)
    {
        DrawTitleButtonBase(r);
        var marco = new Rectangle(r.X + 3, r.Y + 3, r.Width - 6, r.Height - 6);
        Raylib.DrawRectangleLinesEx(marco, 1, Color.Black);
        Raylib.DrawLine((int)(marco.X + marco.Width / 2), (int)marco.Y, (int)(marco.X + marco.Width / 2), (int)(marco.Y + marco.Height), Color.Black);
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
