using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class DialogoModal
{
    const int AnchoBoton = 70;
    const int AltoBoton = 24;
    const int EspacioBotones = 8;
    const int Padding = 16;
    const int AltoLinea = 20;
    const int AltoTitulo = 24;
    const int GrosorMarco = 4;
    const int LadoRemache = 6;

    public bool Visible { get; private set; }

    public void Mostrar(string titulo, string mensaje, string[] botones, Action<int> alElegir)
    {
        this.titulo = titulo;
        this.mensaje = mensaje;
        this.botones = botones;
        this.alElegir = alElegir;
        Visible = true;
    }

    public void Update()
    {
        if (!Visible) return;

        var mouse = Raylib.GetMousePosition();
        var rects = RectsBotones(AreaVentana());

        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            for (int i = 0; i < rects.Length; i++)
            {
                if (Raylib.CheckCollisionPointRec(mouse, rects[i]))
                {
                    Cerrar(i);
                    return;
                }
            }
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) Cerrar(botones.Length - 1);
    }

    void Cerrar(int indice)
    {
        Visible = false;
        alElegir?.Invoke(indice);
    }

    public void Draw()
    {
        if (!Visible) return;

        Raylib.DrawRectangle(0, 0, Raylib.GetScreenWidth(), Raylib.GetScreenHeight(), new Color(0, 0, 0, 90));

        var area = AreaVentana();
        Raylib.DrawRectangleRec(area, Win95Style.Background);
        Win95Style.DrawBevel(area, sunken: false, thickness: GrosorMarco);

        var areaTitulo = new Rectangle(area.X + GrosorMarco, area.Y + GrosorMarco, area.Width - GrosorMarco * 2, AltoTitulo);
        Raylib.DrawRectangleRec(areaTitulo, Win95Style.TitleBar);
        Win95Style.DrawText(titulo, (int)areaTitulo.X + 6, (int)areaTitulo.Y + 4, 16, Color.White);

        DibujarRemaches(area);

        var lineas = mensaje.Split('\n');
        int yTexto = (int)area.Y + GrosorMarco + AltoTitulo + Padding;
        for (int i = 0; i < lineas.Length; i++)
            Win95Style.DrawText(lineas[i], (int)area.X + Padding, yTexto + i * AltoLinea, 16, Color.Black);

        var mouse = Raylib.GetMousePosition();
        var rects = RectsBotones(area);
        for (int i = 0; i < rects.Length; i++)
        {
            bool hover = Raylib.CheckCollisionPointRec(mouse, rects[i]);
            Raylib.DrawRectangleRec(rects[i], hover ? Color.LightGray : Win95Style.Background);
            Win95Style.DrawBevel(rects[i], sunken: false, thickness: 2);

            int anchoTexto = Win95Style.MeasureText(botones[i], 16);
            Win95Style.DrawText(botones[i], (int)(rects[i].X + (rects[i].Width - anchoTexto) / 2), (int)rects[i].Y + 4, 16, Color.Black);
        }
    }

    // remaches en las 4 esquinas, como el marco de ventana de un juego
    void DibujarRemaches(Rectangle area)
    {
        DibujarRemache(area.X + 3, area.Y + 3);
        DibujarRemache(area.X + area.Width - 3 - LadoRemache, area.Y + 3);
        DibujarRemache(area.X + 3, area.Y + area.Height - 3 - LadoRemache);
        DibujarRemache(area.X + area.Width - 3 - LadoRemache, area.Y + area.Height - 3 - LadoRemache);
    }

    void DibujarRemache(float x, float y)
    {
        Raylib.DrawRectangle((int)x, (int)y, LadoRemache, LadoRemache, Win95Style.BevelDark);
        Raylib.DrawRectangle((int)x, (int)y, LadoRemache - 2, LadoRemache - 2, Win95Style.BevelLight);
    }

    Rectangle AreaVentana()
    {
        var lineas = mensaje.Split('\n');
        int anchoTexto = lineas.Length == 0 ? 0 : lineas.Max(l => Win95Style.MeasureText(l, 16));
        int anchoTitulo = Win95Style.MeasureText(titulo, 16) + 40;
        int ancho = Math.Max(280, Math.Max(anchoTexto, anchoTitulo) + Padding * 2);
        int alto = GrosorMarco + AltoTitulo + Padding + lineas.Length * AltoLinea + Padding + AltoBoton + Padding;
        float x = (Raylib.GetScreenWidth() - ancho) / 2f;
        float y = (Raylib.GetScreenHeight() - alto) / 2f;
        return new Rectangle(x, y, ancho, alto);
    }

    Rectangle[] RectsBotones(Rectangle area)
    {
        var rects = new Rectangle[botones.Length];
        float anchoTotal = botones.Length * AnchoBoton + (botones.Length - 1) * EspacioBotones;
        float x = area.X + (area.Width - anchoTotal) / 2;
        float y = area.Y + area.Height - Padding - AltoBoton;
        for (int i = 0; i < botones.Length; i++)
        {
            rects[i] = new Rectangle(x, y, AnchoBoton, AltoBoton);
            x += AnchoBoton + EspacioBotones;
        }
        return rects;
    }

    string titulo = "";
    string mensaje = "";
    string[] botones = Array.Empty<string>();
    Action<int>? alElegir;
}
