using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class ScrollBarWin95
{
    const int LadoBoton = 16;

    public Rectangle Bounds;
    public float Valor; // 0 = arriba del todo, 1 = abajo del todo
    public float ProporcionThumb = 0.3f;

    bool arrastrandoThumb;
    float mouseYAlIniciarArrastre;
    float valorAlIniciarArrastre;

    Rectangle BotonArriba => new(Bounds.X, Bounds.Y, Bounds.Width, LadoBoton);
    Rectangle BotonAbajo => new(Bounds.X, Bounds.Y + Bounds.Height - LadoBoton, Bounds.Width, LadoBoton);
    Rectangle Pista => new(Bounds.X, Bounds.Y + LadoBoton, Bounds.Width, Bounds.Height - LadoBoton * 2);

    Rectangle ThumbBounds()
    {
        float altoThumb = MathF.Max(LadoBoton, Pista.Height * ProporcionThumb);
        float yThumb = Pista.Y + (Pista.Height - altoThumb) * Valor;
        return new Rectangle(Pista.X, yThumb, Pista.Width, altoThumb);
    }

    public void Update()
    {
        if (WasClicked(BotonArriba)) Valor = Math.Max(0f, Valor - 0.1f);
        if (WasClicked(BotonAbajo)) Valor = Math.Min(1f, Valor + 0.1f);

        var thumbBounds = ThumbBounds();

        if (!arrastrandoThumb && Raylib.IsMouseButtonPressed(MouseButton.Left)
            && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), thumbBounds))
        {
            arrastrandoThumb = true;
            mouseYAlIniciarArrastre = Raylib.GetMousePosition().Y;
            valorAlIniciarArrastre = Valor;
        }

        if (!arrastrandoThumb) return;

        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            arrastrandoThumb = false;
            return;
        }

        float alturaDisponible = Pista.Height - thumbBounds.Height;
        if (alturaDisponible <= 0) return;

        float deltaY = Raylib.GetMousePosition().Y - mouseYAlIniciarArrastre;
        Valor = Math.Clamp(valorAlIniciarArrastre + deltaY / alturaDisponible, 0f, 1f);
    }

    public void Draw()
    {
        Raylib.DrawRectangleRec(Pista, Win95Style.Background);
        Win95Style.DrawBevel(Pista, sunken: true, thickness: 1);

        DibujarBotonFlecha(BotonArriba, arribaOAbajo: true);
        DibujarBotonFlecha(BotonAbajo, arribaOAbajo: false);

        var thumbBounds = ThumbBounds();
        Raylib.DrawRectangleRec(thumbBounds, Win95Style.Background);
        Win95Style.DrawBevel(thumbBounds, sunken: false, thickness: 1);
    }

    void DibujarBotonFlecha(Rectangle r, bool arribaOAbajo)
    {
        Win95Style.DrawTitleButtonBase(r);

        float cx = r.X + r.Width / 2;
        float cy = r.Y + r.Height / 2;
        
        if (arribaOAbajo)
            Raylib.DrawTriangle(new(cx, cy - 4), new(cx - 4, cy + 3), new(cx + 4, cy + 3), Color.Black);
        else
            Raylib.DrawTriangle(new(cx, cy + 4), new(cx + 4, cy - 3), new(cx - 4, cy - 3), Color.Black);
    }

    bool WasClicked(Rectangle r)
    {
        return Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r) && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }
}
