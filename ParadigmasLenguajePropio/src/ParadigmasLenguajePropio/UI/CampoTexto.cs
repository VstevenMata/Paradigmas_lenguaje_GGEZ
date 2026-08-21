using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

// solo junta lo que se escribe, no interpreta ni valida nada (eso lo hace quien lo use)
public class CampoTexto
{
    const int FontSize = 14;

    public void Update()
    {
        if (Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            bool dentro = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds);
            TieneFoco = dentro;
            if (dentro) cursor = ColumnaEnX(Raylib.GetMousePosition().X - (Bounds.X + 4));
        }

        if (!TieneFoco) return;

        int tecla = Raylib.GetCharPressed();
        while (tecla > 0)
        {
            if (tecla >= 32 && tecla != 127)
            {
                Texto = Texto.Insert(cursor, ((char)tecla).ToString());
                cursor++;
            }
            tecla = Raylib.GetCharPressed();
        }

        if (Pulsado(KeyboardKey.Backspace) && cursor > 0) { Texto = Texto.Remove(cursor - 1, 1); cursor--; }
        if (Pulsado(KeyboardKey.Delete) && cursor < Texto.Length) Texto = Texto.Remove(cursor, 1);
        if (Pulsado(KeyboardKey.Left) && cursor > 0) cursor--;
        if (Pulsado(KeyboardKey.Right) && cursor < Texto.Length) cursor++;
        if (Pulsado(KeyboardKey.Home)) cursor = 0;
        if (Pulsado(KeyboardKey.End)) cursor = Texto.Length;

        cursor = Math.Clamp(cursor, 0, Texto.Length);
    }

    // dibujarFondo en false si quien lo usa ya dibujó su propio bisel alrededor, para no anidar dos
    public void Draw(string placeholder, bool dibujarFondo = true)
    {
        if (dibujarFondo)
        {
            Raylib.DrawRectangleRec(Bounds, Color.White);
            Win95Style.DrawBevel(Bounds, sunken: true, thickness: 2);
        }

        Raylib.BeginScissorMode((int)Bounds.X + 2, (int)Bounds.Y, (int)Bounds.Width - 4, (int)Bounds.Height);

        int yTexto = (int)(Bounds.Y + (Bounds.Height - FontSize) / 2);
        if (Texto.Length == 0 && !TieneFoco)
        {
            Win95Style.DrawText(placeholder, (int)Bounds.X + 4, yTexto, FontSize, Win95Style.TextoDeshabilitado);
        }
        else
        {
            Win95Style.DrawText(Texto, (int)Bounds.X + 4, yTexto, FontSize, Color.Black);
            if (TieneFoco && Raylib.GetTime() % 1.0 < 0.5)
            {
                int xCursor = (int)Bounds.X + 4 + Win95Style.MeasureText(Texto[..cursor], FontSize);
                Raylib.DrawLine(xCursor, (int)Bounds.Y + 3, xCursor, (int)(Bounds.Y + Bounds.Height - 3), Color.Black);
            }
        }

        Raylib.EndScissorMode();
    }

    int ColumnaEnX(float xRelativo)
    {
        int anchoPrevio = 0;
        for (int c = 0; c < Texto.Length; c++)
        {
            int anchoActual = Win95Style.MeasureText(Texto[..(c + 1)], FontSize);
            if (xRelativo < (anchoPrevio + anchoActual) / 2f) return c;
            anchoPrevio = anchoActual;
        }
        return Texto.Length;
    }

    static bool Pulsado(KeyboardKey tecla) => Raylib.IsKeyPressed(tecla) || Raylib.IsKeyPressedRepeat(tecla);

    public Rectangle Bounds;
    public string Texto = "";
    public bool TieneFoco;

    int cursor;
}
