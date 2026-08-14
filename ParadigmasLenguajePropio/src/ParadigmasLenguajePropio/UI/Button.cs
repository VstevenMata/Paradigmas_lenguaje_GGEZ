using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class Button
{
    public Rectangle Bounds;
    public string Text;
    public Color BackgroundColor;
    public Color TextColor;
    public Texture2D? Icon;

    public Button(int x, int y, int width, int height, string text)
    {
        Bounds = new Rectangle(x, y, width, height);
        Text = text;
        BackgroundColor = Win95Style.Background;
        TextColor = Color.Black;
    }

    public bool IsHovered()
    {
        return Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
    }

    public bool IsPressed()
    {
        return IsHovered() && Raylib.IsMouseButtonDown(MouseButton.Left);
    }

    public bool IsClicked()
    {
        return IsHovered() && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public void Draw()
    {
        bool presionado = IsPressed();
        var color = IsHovered() ? Color.LightGray : BackgroundColor;
        Raylib.DrawRectangleRec(Bounds, color);
        Win95Style.DrawBevel(Bounds, sunken: presionado);

        // Al hacer click se hunde
        var desplazamiento = presionado ? new Vector2(1, 1) : Vector2.Zero;

        if (Icon.HasValue)
        {
            var icon = Icon.Value;

            float escalaMaxima = Math.Min((Bounds.Width - 2) / icon.Width, (Bounds.Height - 2) / icon.Height);
            float escala = escalaMaxima >= 1f ? MathF.Floor(escalaMaxima) : 1f / MathF.Ceiling(1f / escalaMaxima);
            float anchoIcono = icon.Width * escala;
            float altoIcono = icon.Height * escala;
            var posicion = new Vector2(Bounds.X + (Bounds.Width - anchoIcono) / 2, Bounds.Y + (Bounds.Height - altoIcono) / 2) + desplazamiento;
            posicion = new Vector2(MathF.Round(posicion.X), MathF.Round(posicion.Y));
            Raylib.DrawTextureEx(icon, posicion, 0f, escala, Color.White);
            return;
        }

        int fontSize = 20;
        int textWidth = Raylib.MeasureText(Text, fontSize);
        var posicionTexto = new Vector2(Bounds.X + (Bounds.Width - textWidth) / 2, Bounds.Y + (Bounds.Height - fontSize) / 2) + desplazamiento;
        Raylib.DrawText(Text, (int)posicionTexto.X, (int)posicionTexto.Y, fontSize, TextColor);
    }
}
