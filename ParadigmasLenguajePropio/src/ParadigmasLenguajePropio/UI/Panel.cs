using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class Panel
{
    const int TitleBarHeight = 22;
    const int TitleButtonSize = 16;

    public Rectangle Bounds;
    public string Title;
    public bool Visible = true;
    public bool Minimized = false;
    public bool Maximized = false;

    Rectangle boundsBeforeMaximize;

    Rectangle TitleBarBounds => new(Bounds.X, Bounds.Y, Bounds.Width, TitleBarHeight);

    Rectangle CloseButton => new(Bounds.X + Bounds.Width - TitleButtonSize - 4, Bounds.Y + 3, TitleButtonSize, TitleButtonSize);
    Rectangle MaximizeButton => new(CloseButton.X - TitleButtonSize - 5, Bounds.Y + 3, TitleButtonSize, TitleButtonSize);
    Rectangle MinimizeButton => new(MaximizeButton.X - TitleButtonSize - 5, Bounds.Y + 3, TitleButtonSize, TitleButtonSize);

    public Panel(int x, int y, int width, int height, string title)
    {
        Bounds = new Rectangle(x, y, width, height);
        Title = title;
    }

    public void Update()
    {
        if (!Visible) return;

        if (WasClicked(CloseButton))
        {
            Visible = false;
            return;
        }

        if (WasClicked(MinimizeButton))
        {
            Minimized = !Minimized;
        }

        if (WasClicked(MaximizeButton))
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
                var contentArea = new Rectangle(
                    Bounds.X + 4, Bounds.Y + TitleBarHeight + 4,
                    Bounds.Width - 8, Bounds.Height - TitleBarHeight - 8);
                drawContent(contentArea);
            }
        }

        Raylib.DrawRectangleRec(TitleBarBounds, Win95Style.TitleBar);
        Raylib.DrawText(Title, (int)Bounds.X + 4, (int)Bounds.Y + 4, 14, Color.White);

        Win95Style.DrawMinimizeIcon(MinimizeButton);
        Win95Style.DrawMaximizeIcon(MaximizeButton);
        Win95Style.DrawCloseIcon(CloseButton);
    }

    bool WasClicked(Rectangle r)
    {
        return Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r) && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }
}
