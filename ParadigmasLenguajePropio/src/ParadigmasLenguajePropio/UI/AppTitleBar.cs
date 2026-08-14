using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class AppTitleBar
{
    public const int Height = 26;
    const int ButtonSize = 16;

    public string Title;
    public bool ShouldClose { get; private set; }

    bool arrastrando;
    Vector2 mouseGlobalAlIniciarArrastre;
    Vector2 posicionVentanaAlIniciarArrastre;

    Rectangle Bounds => new(0, 0, Raylib.GetScreenWidth(), Height);
    Rectangle CloseButton => new(Bounds.Width - ButtonSize - 4, 3, ButtonSize, ButtonSize);
    Rectangle MaximizeButton => new(CloseButton.X - ButtonSize - 5, 3, ButtonSize, ButtonSize);
    Rectangle MinimizeButton => new(MaximizeButton.X - ButtonSize - 5, 3, ButtonSize, ButtonSize);

    public AppTitleBar(string title)
    {
        Title = title;
    }

    public void Update()
    {
        if (WasClicked(CloseButton))
        {
            ShouldClose = true;
            return;
        }

        if (WasClicked(MinimizeButton))
        {
            Raylib.MinimizeWindow();
            return;
        }

        if (WasClicked(MaximizeButton))
        {
            if (Raylib.IsWindowMaximized()) Raylib.RestoreWindow();
            else Raylib.MaximizeWindow();
            return;
        }

        UpdateArrastre();
    }

    void UpdateArrastre()
    {
        bool sobreBoton = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), CloseButton)
            || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), MaximizeButton)
            || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), MinimizeButton);

        if (!arrastrando && !sobreBoton && Raylib.IsMouseButtonPressed(MouseButton.Left)
            && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds))
        {
            arrastrando = true;
            posicionVentanaAlIniciarArrastre = Raylib.GetWindowPosition();
            mouseGlobalAlIniciarArrastre = posicionVentanaAlIniciarArrastre + Raylib.GetMousePosition();
        }

        if (!arrastrando) return;

        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            arrastrando = false;
            return;
        }

        // Sin decoración nativa no hay barra que el SO arrastre solo: "posición de ventana +
        // mouse local" da la posición global del cursor, y ese valor no cambia si lo único que
        // se movió fue la ventana (el mouse local se corrió exactamente lo mismo al revés).
        Vector2 mouseGlobalActual = Raylib.GetWindowPosition() + Raylib.GetMousePosition();
        Vector2 delta = mouseGlobalActual - mouseGlobalAlIniciarArrastre;
        Vector2 nuevaPosicion = posicionVentanaAlIniciarArrastre + delta;
        Raylib.SetWindowPosition((int)nuevaPosicion.X, (int)nuevaPosicion.Y);
    }

    bool WasClicked(Rectangle r)
    {
        return Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r) && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public void Draw()
    {
        Raylib.DrawRectangleRec(Bounds, Win95Style.TitleBar);
        Raylib.DrawText(Title, 6, 5, 14, Color.White);

        Win95Style.DrawMinimizeIcon(MinimizeButton);
        Win95Style.DrawMaximizeIcon(MaximizeButton);
        Win95Style.DrawCloseIcon(CloseButton);
    }
}
