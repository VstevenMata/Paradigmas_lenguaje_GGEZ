using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class ScrollBarWin95
{
    const int LadoBoton = 16;
    const int MargenThumb = 3; // separa el thumb de los lados de la pista, para que quede fino y flote como un pixel gris
    const int MargenExtremos = 3;

    Rectangle ThumbBounds()
    {
        if (Horizontal)
        {
            float pistaUtil = Pista.Width - MargenExtremos * 2;
            float anchoThumb = MathF.Max(LadoBoton, pistaUtil * ProporcionThumb);
            float xThumb = Pista.X + MargenExtremos + (pistaUtil - anchoThumb) * Valor;
            return new Rectangle(xThumb, Pista.Y + MargenThumb, anchoThumb, Pista.Height - MargenThumb * 2);
        }

        float pistaUtilV = Pista.Height - MargenExtremos * 2;
        float altoThumb = MathF.Max(LadoBoton, pistaUtilV * ProporcionThumb);
        float yThumb = Pista.Y + MargenExtremos + (pistaUtilV - altoThumb) * Valor;
        return new Rectangle(Pista.X + MargenThumb, yThumb, Pista.Width - MargenThumb * 2, altoThumb);
    }

    public void Update()
    {
        if (WasClicked(BotonInicio)) Valor = Math.Max(0f, Valor - 0.1f);
        if (WasClicked(BotonFin)) Valor = Math.Min(1f, Valor + 0.1f);

        var thumbBounds = ThumbBounds();

        if (!arrastrandoThumb && Raylib.IsMouseButtonPressed(MouseButton.Left)
            && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), thumbBounds))
        {
            arrastrandoThumb = true;
            mouseAlIniciarArrastre = Horizontal ? Raylib.GetMousePosition().X : Raylib.GetMousePosition().Y;
            valorAlIniciarArrastre = Valor;
        }

        if (!arrastrandoThumb) return;

        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            arrastrandoThumb = false;
            return;
        }

        float pistaUtil = (Horizontal ? Pista.Width : Pista.Height) - MargenExtremos * 2;
        float disponible = Horizontal ? pistaUtil - thumbBounds.Width : pistaUtil - thumbBounds.Height;
        if (disponible <= 0) return;

        float mouseActual = Horizontal ? Raylib.GetMousePosition().X : Raylib.GetMousePosition().Y;
        float delta = mouseActual - mouseAlIniciarArrastre;
        Valor = Math.Clamp(valorAlIniciarArrastre + delta / disponible, 0f, 1f);
    }

    public void Draw()
    {
        // trama a cuadros clásica de Win95/98 en la pista, como el scrollbar del tema retro de Obsidian
        Win95Style.DrawDither(Pista, Color.White, Win95Style.Background);

        DibujarBotonFlecha(BotonInicio, inicio: true);
        DibujarBotonFlecha(BotonFin, inicio: false);

        // thumb fino y plano: un solo pixel de relieve, como el indicador gris del tema retro de Obsidian
        var thumbBounds = ThumbBounds();
        bool resaltado = arrastrandoThumb || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), thumbBounds);
        Raylib.DrawRectangleRec(thumbBounds, resaltado ? Color.LightGray : Win95Style.ScrollTrack);
        Win95Style.DrawBevel(thumbBounds, sunken: false, thickness: 1);
    }

    void DibujarBotonFlecha(Rectangle r, bool inicio)
    {
        Win95Style.DrawTitleButtonBase(r);

        float cx = r.X + r.Width / 2;
        float cy = r.Y + r.Height / 2;

        if (Horizontal)
        {
            // mismo orden de vértices que las flechas verticales, para que el relleno se comporte igual en la GPU
            if (inicio)
                Raylib.DrawTriangle(new(cx - 4, cy), new(cx + 3, cy + 4), new(cx + 3, cy - 4), Color.Black);
            else
                Raylib.DrawTriangle(new(cx + 4, cy), new(cx - 3, cy - 4), new(cx - 3, cy + 4), Color.Black);
        }
        else
        {
            if (inicio)
                Raylib.DrawTriangle(new(cx, cy - 4), new(cx - 4, cy + 3), new(cx + 4, cy + 3), Color.Black);
            else
                Raylib.DrawTriangle(new(cx, cy + 4), new(cx + 4, cy - 3), new(cx - 4, cy - 3), Color.Black);
        }
    }

    bool WasClicked(Rectangle r)
    {
        return Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r) && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public Rectangle Bounds;
    public bool Horizontal;
    public float Valor; // 0 = al inicio, 1 = al final
    public float ProporcionThumb = 0.3f;

    bool arrastrandoThumb;
    float mouseAlIniciarArrastre;
    float valorAlIniciarArrastre;

    Rectangle BotonInicio => Horizontal
        ? new Rectangle(Bounds.X, Bounds.Y, LadoBoton, Bounds.Height)
        : new Rectangle(Bounds.X, Bounds.Y, Bounds.Width, LadoBoton);

    Rectangle BotonFin => Horizontal
        ? new Rectangle(Bounds.X + Bounds.Width - LadoBoton, Bounds.Y, LadoBoton, Bounds.Height)
        : new Rectangle(Bounds.X, Bounds.Y + Bounds.Height - LadoBoton, Bounds.Width, LadoBoton);

    Rectangle Pista => Horizontal
        ? new Rectangle(Bounds.X + LadoBoton, Bounds.Y, Bounds.Width - LadoBoton * 2, Bounds.Height)
        : new Rectangle(Bounds.X, Bounds.Y + LadoBoton, Bounds.Width, Bounds.Height - LadoBoton * 2);
}
