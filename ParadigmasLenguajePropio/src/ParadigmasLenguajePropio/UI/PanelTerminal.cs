using Raylib_cs;
namespace ParadigmasLenguajePropio.UI;

public class PanelTerminal
{
    const int AltoTabs = 22;
    const int AnchoScroll = 16;
    const int AnchoTab = 70;
    const int MargenArribaTabs = 4;
    const int MargenDebajoTabs = 4;
    static readonly Color ColorPrompt = Color.Lime;
    static readonly Color ColorEscrito = Color.White;

    public void Update()
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tabRect = new Rectangle(Bounds.X + i * AnchoTab, Bounds.Y + MargenArribaTabs, AnchoTab, AltoTabs);
            if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), tabRect) && Raylib.IsMouseButtonPressed(MouseButton.Left))
                TabActiva = i;
        }

        if (TabActiva == 0 && TieneFoco) ManejarTeclado();

        var areaContenido = AreaContenido();
        scroll.Bounds = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y, AnchoScroll, areaContenido.Height);
        scroll.Update();
    }

    void ManejarTeclado()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressedRepeat(KeyboardKey.Enter))
        {
            LineasTerminal.Add((PromptActual(), entradaActual));
            entradaActual = "";
            return;
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && entradaActual.Length > 0)
            entradaActual = entradaActual[..^1];

        int tecla = Raylib.GetCharPressed();
        while (tecla > 0)
        {
            entradaActual += (char)tecla;
            tecla = Raylib.GetCharPressed();
        }
    }

    public void Draw()
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tabRect = new Rectangle(Bounds.X + i * AnchoTab, Bounds.Y + MargenArribaTabs, AnchoTab, AltoTabs);
            bool activa = i == TabActiva;
            Raylib.DrawRectangleRec(tabRect, Win95Style.Background);
            Win95Style.DrawBevel(tabRect, sunken: !activa, thickness: 2);
            Win95Style.DrawText(Tabs[i], (int)tabRect.X + 8, (int)tabRect.Y + 5, 16, Color.Black);
        }

        var areaContenido = AreaContenido();
        Raylib.DrawRectangleRec(areaContenido, Color.Black);
        Win95Style.DrawBevel(areaContenido, sunken: true, thickness: 4);

        bool esTerminal = TabActiva == 0;
        // en la pestaña Terminal hay una línea extra: la viva, donde todavía se puede seguir escribiendo
        int totalLineas = esTerminal ? LineasTerminal.Count + 1 : LineasErrores.Count;

        const int altoLinea = 18;
        int lineasVisibles = Math.Max(1, (int)(areaContenido.Height - 10) / altoLinea);
        int primeraLinea = Math.Max(0, totalLineas - lineasVisibles);

        Raylib.BeginScissorMode((int)areaContenido.X + 2, (int)areaContenido.Y + 2, (int)areaContenido.Width - 4, (int)areaContenido.Height - 4);
        for (int i = primeraLinea; i < totalLineas; i++)
        {
            int y = (int)areaContenido.Y + 10 + (i - primeraLinea) * altoLinea;
            int x = (int)areaContenido.X + 10;

            if (!esTerminal)
            {
                Win95Style.DrawText(LineasErrores[i], x, y, 16, Color.White);
                continue;
            }

            bool esLaLineaViva = i == LineasTerminal.Count;
            var (prompt, contenido) = esLaLineaViva ? (PromptActual(), entradaActual) : LineasTerminal[i];

            Win95Style.DrawText(prompt, x, y, 16, ColorPrompt);
            int xContenido = x + Win95Style.MeasureText(prompt, 16);
            Win95Style.DrawText(contenido, xContenido, y, 16, ColorEscrito);

            if (esLaLineaViva)
            {
                int xCursor = xContenido + Win95Style.MeasureText(contenido, 16);
                // sólido y parpadeante con foco; hueco y fijo sin foco
                if (TieneFoco)
                {
                    if (Raylib.GetTime() % 1.0 < 0.5) Raylib.DrawRectangle(xCursor, y, 8, 16, ColorEscrito);
                }
                else
                {
                    Raylib.DrawRectangleLines(xCursor, y, 8, 16, ColorEscrito);
                }
            }
        }
        Raylib.EndScissorMode();

        scroll.Draw();

        Win95Style.DrawDivisorVertical((int)(areaContenido.X + areaContenido.Width) + 1, (int)areaContenido.Y, (int)areaContenido.Height, thickness: 2);
    }

    Rectangle AreaContenido()
    {
        float y = Bounds.Y + MargenArribaTabs + AltoTabs + MargenDebajoTabs;
        return new Rectangle(
            Bounds.X, y,
            Bounds.Width - 4 - AnchoScroll, Bounds.Y + Bounds.Height - y);
    }

    // al abrir un archivo el prompt pasa a esa carpeta, como el cwd de una terminal real
    public void RegistrarAperturaArchivo(string rutaCompleta)
    {
        string carpeta = Path.GetDirectoryName(rutaCompleta) ?? "";
        rutaActual = RutaCorta(carpeta);
    }

    // usuario@host fijos, la ruta se actualiza con el último archivo abierto
    string PromptActual() => $"{Environment.UserName}@{Environment.MachineName.ToLowerInvariant()}:{rutaActual}$ ";

    // reemplaza el home real por "~"
    static string RutaCorta(string ruta)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrEmpty(home) && ruta.StartsWith(home) ? "~" + ruta[home.Length..] : ruta;
    }

    public Rectangle Bounds;
    public string[] Tabs = { "Terminal", "Errores" };
    public int TabActiva;
    public bool TieneFoco; // true cuando se clickeó dentro de la terminal si no, el teclado es del editor
    public List<(string Prompt, string Contenido)> LineasTerminal = new();
    public List<string> LineasErrores = new() { "Los errores no existen" };

    readonly ScrollBarWin95 scroll = new();
    string entradaActual = ""; // lo que se va escribiendo en la línea viva, justo después de la parte fija
    string rutaActual = "~"; // se actualiza cada vez que se abre un archivo
}
