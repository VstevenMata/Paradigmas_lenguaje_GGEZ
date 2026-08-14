using Raylib_cs;
namespace ParadigmasLenguajePropio.UI;

public class PanelTerminal
{
    // VARIABLES CONSTANTES
    const int AltoTabs = 18;
    const int AnchoScroll = 16;
    const int AnchoTab = 70;
    const int MargenArribaTabs = 4;
    const int MargenDebajoTabs = 4;

    public void Update()
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tabRect = new Rectangle(Bounds.X + i * AnchoTab, Bounds.Y + MargenArribaTabs, AnchoTab, AltoTabs);
            if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), tabRect) && Raylib.IsMouseButtonPressed(MouseButton.Left))
                TabActiva = i;
        }

        var areaContenido = AreaContenido();
        scroll.Bounds = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y, AnchoScroll, areaContenido.Height);
        scroll.Update();
    }

    public void Draw()
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tabRect = new Rectangle(Bounds.X + i * AnchoTab, Bounds.Y + MargenArribaTabs, AnchoTab, AltoTabs);
            bool activa = i == TabActiva;
            Raylib.DrawRectangleRec(tabRect, Win95Style.Background);
            Win95Style.DrawBevel(tabRect, sunken: !activa, thickness: 2);
            Win95Style.DrawText(Tabs[i], (int)tabRect.X + 8, (int)tabRect.Y + 5, 12, Color.Black);
        }

        var areaContenido = AreaContenido();
        Raylib.DrawRectangleRec(areaContenido, Color.Black);
        Win95Style.DrawBevel(areaContenido, sunken: true, thickness: 4);

        var lineas = TabActiva == 0 ? LineasTerminal : LineasErrores;
        var colorTexto = TabActiva == 0 ? Color.Lime : Color.White;
        for (int i = 0; i < lineas.Count; i++)
        {
            Win95Style.DrawText(lineas[i], (int)areaContenido.X + 10, (int)areaContenido.Y + 10 + i * 14, 12, colorTexto);
        }

        scroll.Draw();

        // línea al frente, encima del scroll, igual que en el explorador
        Win95Style.DrawDivisorVertical((int)(areaContenido.X + areaContenido.Width) + 1, (int)areaContenido.Y, (int)areaContenido.Height, thickness: 2);
    }

    Rectangle AreaContenido()
    {
        float y = Bounds.Y + MargenArribaTabs + AltoTabs + MargenDebajoTabs;
        return new Rectangle(
            Bounds.X, y,
            Bounds.Width - 4 - AnchoScroll, Bounds.Y + Bounds.Height - y);
    }

    public Rectangle Bounds;
    public string[] Tabs = { "Terminal", "Errores" };
    public int TabActiva;
    public List<string> LineasTerminal = new() { "stevenmq@fedora: cd Paradigmas_lenguaje_GGEZ", "stevenmq@fedora:~/Paradigmas_lenguaje_GGEZ$ nix run .#up" };
    public List<string> LineasErrores = new() { "Los errores no existen" };

    readonly ScrollBarWin95 scroll = new();
}
