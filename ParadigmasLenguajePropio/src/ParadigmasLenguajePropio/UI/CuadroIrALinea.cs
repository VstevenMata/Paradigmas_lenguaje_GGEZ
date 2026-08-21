using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

// todavía no mueve el cursor del editor: solo captura el número escrito
public class CuadroIrALinea
{
    const int Ancho = 260;
    const int Alto = 56;
    const int Padding = 8;
    const int AltoCampo = 26;

    public void Update()
    {
        if (!Visible) return;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Visible = false; return; }
        campo.Update();
    }

    public void Posicionar(Rectangle areaEditor)
    {
        float x = areaEditor.X + (areaEditor.Width - Ancho) / 2f;
        float y = areaEditor.Y + PanelCentral.AltoCabecera;
        Bounds = new Rectangle(x, y, Ancho, Alto);
        campo.Bounds = new Rectangle(Bounds.X + Padding, Bounds.Y + Padding, Bounds.Width - Padding * 2, AltoCampo);
    }

    public void Draw()
    {
        if (!Visible) return;

        Raylib.DrawRectangleRec(Bounds, Win95Style.Background);
        Win95Style.DrawBevel(Bounds, sunken: false, thickness: 2);

        campo.Draw("Ej: 42");

        Win95Style.DrawText("Escribe un numero de linea y presiona Enter", (int)Bounds.X + Padding, (int)(campo.Bounds.Y + AltoCampo + 4), 12, Color.Black);
    }

    public bool Visible;
    public Rectangle Bounds { get; private set; }
    public bool TieneFocoEnCampo => campo.TieneFoco;

    readonly CampoTexto campo = new();
}
