using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class Button
{
    public Rectangle Bounds;
    public string Text;
    public Color BackgroundColor;
    public Color TextColor;

    public Button(int x, int y, int width, int height, string text)
    {
        Bounds = new Rectangle(x, y, width, height);
        Text = text;
        BackgroundColor = Color.LightGray;
        TextColor = Color.Black;
    }

    public bool IsHovered()
    {
        return Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
    }

    public bool IsClicked()
    {
        return IsHovered() && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public void Draw()
    {
        var color = IsHovered() ? Color.Gray : BackgroundColor;
        Raylib.DrawRectangleRec(Bounds, color);
        Raylib.DrawRectangleLinesEx(Bounds, 2, Color.Black);

        int fontSize = 20;
        int textWidth = Raylib.MeasureText(Text, fontSize);
        int textX = (int)(Bounds.X + (Bounds.Width - textWidth) / 2);
        int textY = (int)(Bounds.Y + (Bounds.Height - fontSize) / 2);
        Raylib.DrawText(Text, textX, textY, fontSize, TextColor);
    }
}
