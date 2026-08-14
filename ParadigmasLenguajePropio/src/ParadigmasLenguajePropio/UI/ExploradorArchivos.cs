using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class ExploradorArchivos
{
    // VARIABLES CONSTANTES
    const int AltoTitulo = 18;
    const int AnchoScroll = 16;

    public void Update()
    {
        var areaContenido = AreaContenido();
        scroll.Bounds = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y, AnchoScroll, areaContenido.Height);
        scroll.Update();
    }

    public void Draw()
    {
        // sin marco propio el panel flota directo sobre el gris compartido de la app

        Win95Style.DrawText(Titulo, (int)Bounds.X + 6, (int)Bounds.Y + 4, 12, Color.Black);

        var areaContenido = AreaContenido();
        Raylib.DrawRectangleRec(areaContenido, Color.White);
        Win95Style.DrawBevel(areaContenido, sunken: true, thickness: 3);

        scroll.Draw();

        // línea que separa el panel blanco de la scrollbar al frente, encima de todo lo anterior
        Win95Style.DrawDivisorVertical((int)(areaContenido.X + areaContenido.Width) + 1, (int)areaContenido.Y, (int)areaContenido.Height, thickness: 2);
    }

    Rectangle AreaContenido()
    {
        return new Rectangle(
            Bounds.X + 2, Bounds.Y + AltoTitulo,
            Bounds.Width - 6 - AnchoScroll, Bounds.Height - AltoTitulo - 2);
    }

    public Rectangle Bounds;
    public string Titulo = "Explorador";

    readonly ScrollBarWin95 scroll = new();
}
