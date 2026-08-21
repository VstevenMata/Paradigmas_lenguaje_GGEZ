using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class Panel
{
    public const int TitleBarHeight = 26;
    const int TitleButtonSize = 16;

    public Panel(int x, int y, int width, int height, string title)
    {
        Bounds = new Rectangle(x, y, width, height);
        Title = title;
    }

    public void Update()
    {
        if (!Visible) return;

        if (MostrarCerrar && WasClicked(CloseButton))
        {
            Visible = false;
            return;
        }

        if (MostrarMinimizar && WasClicked(MinimizeButton))
        {
            Minimized = !Minimized;
        }

        if (MostrarMaximizar && WasClicked(MaximizeButton))
        {
            Maximized = !Maximized;
            if (Maximized)
            {
                boundsBeforeMaximize = Bounds;
                Bounds = new Rectangle(20, 20, Raylib.GetScreenWidth() - 40, Raylib.GetScreenHeight() - 40);
            }
            else
            {
                Bounds = boundsBeforeMaximize;
            }
        }
    }

    public void Draw(Action<Rectangle>? drawContent = null)
    {
        if (!Visible) return;

        if (!Minimized)
        {
            Raylib.DrawRectangleRec(Bounds, Win95Style.Background);
            Win95Style.DrawBevel(Bounds, sunken: false);

            if (drawContent != null)
            {
                drawContent(ContentArea());
            }
        }

        Raylib.DrawRectangleRec(TitleBarBounds, Win95Style.TitleBar);

        if (!Minimized)
        {
            int xTitulo = (int)Bounds.X + 4;
            if (IconoTitulo != null)
            {
                IconoTitulo(new Rectangle(Bounds.X + 3, Bounds.Y + 3, 16, 16));
                xTitulo += 20;
            }
            Win95Style.DrawText(Title, xTitulo, (int)Bounds.Y + 4, 18, Color.White);
        }

        if (MostrarMinimizar) Win95Style.DrawMinimizeIcon(MinimizeButton);
        if (MostrarMaximizar) Win95Style.DrawMaximizeIcon(MaximizeButton);
        if (MostrarCerrar) Win95Style.DrawCloseIcon(CloseButton);
        if (MostrarBotonExtra)
        {
            Win95Style.DrawTitleButtonBase(BotonExtra);
            IconoBotonExtra?.Invoke(BotonExtra);
        }
    }

    public bool BotonExtraClickeado()
    {
        return Visible && MostrarBotonExtra && WasClicked(BotonExtra);
    }

    public Rectangle ContentArea()
    {
        return new Rectangle(
            Bounds.X + 4, Bounds.Y + TitleBarHeight + 4,
            Bounds.Width - 8, Bounds.Height - TitleBarHeight - 8);
    }

    bool WasClicked(Rectangle r)
    {
        return Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r) && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public Rectangle Bounds;
    public string Title;
    public bool Visible = true;
    public bool Minimized = false;
    public bool Maximized = false;
    public Action<Rectangle>? IconoTitulo;
    public bool MostrarMinimizar = true;
    public bool MostrarMaximizar = true;
    public bool MostrarCerrar = true;
    public bool MostrarBotonExtra = false;
    public Action<Rectangle>? IconoBotonExtra;

    Rectangle boundsBeforeMaximize;

    Rectangle TitleBarBounds => new(Bounds.X, Bounds.Y, Bounds.Width, TitleBarHeight);

    // los botones se acomodan desde la derecha; si alguno está oculto, el siguiente ocupa su lugar
    Rectangle CloseButton => PosicionBoton(0);
    Rectangle MaximizeButton => PosicionBoton(MostrarCerrar ? 1 : 0);
    Rectangle MinimizeButton => PosicionBoton((MostrarCerrar ? 1 : 0) + (MostrarMaximizar ? 1 : 0));
    Rectangle BotonExtra => PosicionBoton((MostrarCerrar ? 1 : 0) + (MostrarMaximizar ? 1 : 0) + (MostrarMinimizar ? 1 : 0));

    Rectangle PosicionBoton(int indiceDesdeLaDerecha)
    {
        float x = Bounds.X + Bounds.Width - TitleButtonSize - 4 - indiceDesdeLaDerecha * (TitleButtonSize + 5);
        return new Rectangle(x, Bounds.Y + 3, TitleButtonSize, TitleButtonSize);
    }
}
