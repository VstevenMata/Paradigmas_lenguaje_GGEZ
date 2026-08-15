using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class ScrollBarWin95
{
    // VARIABLES CONSTANTES
    const int LadoBoton = 16;
    const int MargenThumb = 1;
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
        // pista plana, sin relieve: se confunde con el fondo para que el thumb parezca flotar sobre la nada
        Raylib.DrawRectangleRec(Pista, Win95Style.Background);

        DibujarBotonFlecha(BotonInicio, inicio: true);
        DibujarBotonFlecha(BotonFin, inicio: false);

        var thumbBounds = ThumbBounds();
        Raylib.DrawRectangleRec(thumbBounds, Win95Style.ScrollTrack);
        Win95Style.DrawBevel(thumbBounds, sunken: false, thickness: 2);
    }

    void DibujarBotonFlecha(Rectangle r, bool inicio)
    {
        Win95Style.DrawTitleButtonBase(r);

        float cx = r.X + r.Width / 2;
        float cy = r.Y + r.Height / 2;

        if (Horizontal)
        {
            // mismo orden de vértices que las flechas verticales (tip, esquina-abajo, esquina-arriba)
            // para que el relleno se comporte igual en el driver de GPU
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
