using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class CuadroIrALinea
{
    const int Ancho = 260;
    const int Alto = 56;
    const int Padding = 8;
    const int AltoCampo = 26;
    const int LadoBotonCerrar = 22;

    public void Update()
    {
        if (!Visible) return;
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Visible = false; return; }
        campo.Update();

        if (botonCerrar.IsClicked()) { Visible = false; return; }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) && int.TryParse(campo.Texto, out int numeroLinea))
        {
            Editor?.IrALinea(numeroLinea);
            Visible = false;
        }
    }

    public PanelCentral? Editor;

    public void Posicionar(Rectangle areaEditor)
    {
        float x = areaEditor.X + (areaEditor.Width - Ancho) / 2f;
        float y = areaEditor.Y + PanelCentral.AltoCabecera;
        Bounds = new Rectangle(x, y, Ancho, Alto);

        float yFila = Bounds.Y + Padding;
        float anchoCampo = Bounds.Width - Padding * 2 - LadoBotonCerrar - 4;
        campo.Bounds = new Rectangle(Bounds.X + Padding, yFila, anchoCampo, AltoCampo);
        botonCerrar.Bounds = new Rectangle(campo.Bounds.X + campo.Bounds.Width + 4, yFila + (AltoCampo - LadoBotonCerrar) / 2f, LadoBotonCerrar, LadoBotonCerrar);
    }

    public void Draw()
    {
        if (!Visible) return;

        Raylib.DrawRectangleRec(Bounds, Win95Style.Background);
        Win95Style.DrawBevel(Bounds, sunken: false, thickness: 2);

        campo.Draw("Ej: 42");

        Raylib.DrawRectangleRec(botonCerrar.Bounds, botonCerrar.IsHovered() ? Color.LightGray : Win95Style.Background);
        Win95Style.DrawBevel(botonCerrar.Bounds, sunken: botonCerrar.IsPressed());
        Win95Style.DrawCloseIcon(new Rectangle(botonCerrar.Bounds.X + 3, botonCerrar.Bounds.Y + 3, LadoBotonCerrar - 6, LadoBotonCerrar - 6));

        Win95Style.DrawText("Escribe un numero de linea y presiona Enter", (int)Bounds.X + Padding, (int)(campo.Bounds.Y + AltoCampo + 4), 12, Color.Black);
    }

    public bool Visible;
    public Rectangle Bounds { get; private set; }
    public bool TieneFocoEnCampo => campo.TieneFoco;

    readonly CampoTexto campo = new() { FiltroCaracter = char.IsDigit };
    readonly Button botonCerrar = new(0, 0, LadoBotonCerrar, LadoBotonCerrar, "");
}
