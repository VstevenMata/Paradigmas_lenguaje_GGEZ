using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class Button
{
    public Button(int x, int y, int width, int height, string text)
    {
        Bounds = new Rectangle(x, y, width, height);
        Text = text;
        BackgroundColor = Win95Style.Background;
        TextColor = Color.Black;
    }

    public bool IsHovered()
    {
        return Habilitado && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
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

        if (Icon.HasValue && string.IsNullOrEmpty(Text))
        {
            DibujarIconoCentrado(desplazamiento);
            return;
        }

        if (Icon.HasValue)
        {
            DibujarIconoYTexto(desplazamiento);
            return;
        }

        DibujarTexto(Bounds.X + (Bounds.Width - Win95Style.MeasureText(Text, FontSize)) / 2, desplazamiento);
    }

    void DibujarIconoCentrado(Vector2 desplazamiento)
    {
        var icon = Icon!.Value;

        float escalaMaxima = Math.Min((Bounds.Width - 2) / icon.Width, (Bounds.Height - 2) / icon.Height);
        float escala = escalaMaxima >= 1f ? MathF.Floor(escalaMaxima) : 1f / MathF.Ceiling(1f / escalaMaxima);
        float anchoIcono = icon.Width * escala;
        float altoIcono = icon.Height * escala;
        var posicion = new Vector2(Bounds.X + (Bounds.Width - anchoIcono) / 2, Bounds.Y + (Bounds.Height - altoIcono) / 2) + desplazamiento;
        posicion = new Vector2(MathF.Round(posicion.X), MathF.Round(posicion.Y));
        // deshabilitado: ícono desvanecido, como los controles inactivos de Win95
        Raylib.DrawTextureEx(icon, posicion, 0f, escala, Habilitado ? Color.White : new Color(255, 255, 255, 100));
    }

    void DibujarIconoYTexto(Vector2 desplazamiento)
    {
        var icon = Icon!.Value;
        float ladoIcono = MathF.Min(Bounds.Height - 6, 12);
        float escala = ladoIcono / icon.Width;
        float anchoIcono = icon.Width * escala;
        float altoIcono = icon.Height * escala;

        int anchoTexto = Win95Style.MeasureText(Text, FontSize);
        float anchoTotal = anchoIcono + EspacioIconoTexto + anchoTexto;
        float xInicio = Bounds.X + (Bounds.Width - anchoTotal) / 2;

        var posicionIcono = new Vector2(xInicio, Bounds.Y + (Bounds.Height - altoIcono) / 2) + desplazamiento;
        posicionIcono = new Vector2(MathF.Round(posicionIcono.X), MathF.Round(posicionIcono.Y));
        Raylib.DrawTextureEx(icon, posicionIcono, 0f, escala, Habilitado ? Color.White : new Color(255, 255, 255, 100));

        DibujarTexto(xInicio + anchoIcono + EspacioIconoTexto, desplazamiento);
    }

    void DibujarTexto(float x, Vector2 desplazamiento)
    {
        var posicionTexto = new Vector2(x, Bounds.Y + (Bounds.Height - FontSize) / 2) + desplazamiento;
        if (Habilitado)
            Win95Style.DrawText(Text, (int)posicionTexto.X, (int)posicionTexto.Y, FontSize, TextColor);
        else
            Win95Style.DrawTextDeshabilitado(Text, (int)posicionTexto.X, (int)posicionTexto.Y, FontSize);
    }

    const int EspacioIconoTexto = 4;

    public Rectangle Bounds;
    public string Text;
    public Color BackgroundColor;
    public Color TextColor;
    public Texture2D? Icon;
    public int FontSize = 24;
    public bool Habilitado = true;
    public string? Tooltip;
}
