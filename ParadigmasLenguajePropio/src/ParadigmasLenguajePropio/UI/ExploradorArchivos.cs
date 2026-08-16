using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class ExploradorArchivos
{
    // VARIABLES CONSTANTES
    const int AltoTitulo = 22;
    const int AnchoScroll = 16;
    const int AltoFila = 22;
    const int IndentacionPorNivel = 14;
    const int LadoIcono = 16;
    const int FontSize = 12;
    static readonly Color ColorGuia = new(190, 190, 190, 255);

    public void Update()
    {
        var areaContenido = AreaContenido();
        ManejarClicks(areaContenido);

        scroll.Bounds = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y, AnchoScroll, areaContenido.Height);
        scroll.Update();
    }

    public void Draw()
    {
        // sin marco propio el panel flota directo sobre el gris compartido de la app
        Win95Style.DrawText(Titulo, (int)Bounds.X + 6, (int)Bounds.Y + 4, 16, Color.Black);

        var areaContenido = AreaContenido();
        Raylib.DrawRectangleRec(areaContenido, Color.White);
        Win95Style.DrawBevel(areaContenido, sunken: true, thickness: 3);

        Raylib.BeginScissorMode((int)areaContenido.X + 2, (int)areaContenido.Y + 2, (int)areaContenido.Width - 4, (int)areaContenido.Height - 4);
        DibujarNodos(areaContenido);
        Raylib.EndScissorMode();

        scroll.Draw();

        // línea que separa el panel blanco de la scrollbar al frente, encima de todo lo anterior
        Win95Style.DrawDivisorVertical((int)(areaContenido.X + areaContenido.Width) + 1, (int)areaContenido.Y, (int)areaContenido.Height, thickness: 2);
    }

    void DibujarNodos(Rectangle area)
    {
        int fila = 0;
        DibujarNodo(raiz, 0, area, ref fila);
    }

    void DibujarNodo(NodoArchivos nodo, int nivel, Rectangle area, ref int fila)
    {
        DibujarFila(nodo, nivel, area, fila);
        fila++;

        if (!nodo.EsCarpeta || !nodo.Expandido || nodo.Hijos.Count == 0) return;

        int filaInicioHijos = fila;
        foreach (var hijo in nodo.Hijos)
            DibujarNodo(hijo, nivel + 1, area, ref fila);

        // guía vertical, como en VS Code conecta la carpeta con todos sus hijos visibles
        float xGuia = area.X + 4 + nivel * IndentacionPorNivel + LadoIcono / 2f;
        float yInicio = area.Y + 2 + filaInicioHijos * AltoFila;
        float yFin = area.Y + 2 + fila * AltoFila;
        Raylib.DrawLine((int)xGuia, (int)yInicio, (int)xGuia, (int)yFin, ColorGuia);
    }

    void DibujarFila(NodoArchivos nodo, int nivel, Rectangle area, int fila)
    {
        float x = area.X + 4 + nivel * IndentacionPorNivel;
        float y = area.Y + 2 + fila * AltoFila;

        var icono = IconoPara(nodo);
        if (icono.HasValue)
        {
            var posicionIcono = new Vector2(x, y + (AltoFila - LadoIcono) / 2f);
            Raylib.DrawTextureEx(icono.Value, posicionIcono, 0f, (float)LadoIcono / icono.Value.Width, Color.White);
        }

        Win95Style.DrawText(nodo.Nombre, (int)(x + LadoIcono + 4), (int)(y + (AltoFila - FontSize) / 2f), FontSize, Color.Black);
    }

    Texture2D? IconoPara(NodoArchivos nodo)
    {
        if (nodo.EsCarpeta) return nodo.Expandido ? IconoCarpetaAbierta : IconoCarpetaCerrada;
        if (nodo.Nombre.EndsWith(".ggez", StringComparison.OrdinalIgnoreCase)) return IconoDocumentoGgez;
        if (EsImagen(nodo.Nombre)) return IconoImagenes;
        if (nodo.Nombre.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return IconoDocumentoMd;
        return IconoDocumento;
    }

    static bool EsImagen(string nombre)
    {
        string[] extensiones = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        return extensiones.Any(ext => nombre.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    void ManejarClicks(Rectangle area)
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        int fila = 0;
        foreach (var (nodo, _) in NodosVisibles())
        {
            var filaRect = new Rectangle(area.X, area.Y + 2 + fila * AltoFila, area.Width, AltoFila);
            if (nodo.EsCarpeta && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), filaRect))
            {
                nodo.Expandido = !nodo.Expandido;
                if (nodo.Expandido) nodo.Expandir();
                return;
            }
            fila++;
        }
    }

    IEnumerable<(NodoArchivos nodo, int nivel)> NodosVisibles() => Recorrer(raiz, 0);

    IEnumerable<(NodoArchivos, int)> Recorrer(NodoArchivos nodo, int nivel)
    {
        yield return (nodo, nivel);
        if (nodo.EsCarpeta && nodo.Expandido)
        {
            foreach (var hijo in nodo.Hijos)
                foreach (var item in Recorrer(hijo, nivel + 1))
                    yield return item;
        }
    }

    Rectangle AreaContenido()
    {
        return new Rectangle(
            Bounds.X + 2, Bounds.Y + AltoTitulo,
            Bounds.Width - 6 - AnchoScroll, Bounds.Height - AltoTitulo - 2);
    }

    public Rectangle Bounds;
    public string Titulo = "Explorador";
    public Texture2D? IconoCarpetaAbierta;
    public Texture2D? IconoCarpetaCerrada;
    public Texture2D? IconoDocumento;
    public Texture2D? IconoDocumentoGgez;
    public Texture2D? IconoImagenes;
    public Texture2D? IconoDocumentoMd;

    readonly ScrollBarWin95 scroll = new();

    readonly NodoArchivos raiz = NodoArchivos.Carpeta("C:\\RUNNER",
        NodoArchivos.Carpeta("SRC",NodoArchivos.Carpeta("UI",
                NodoArchivos.Archivo("Button.cs"),
                NodoArchivos.Archivo("AppTitleBar.cs")),
            NodoArchivos.Archivo("LOBBY.GGEZ"),
            NodoArchivos.Archivo("SPRITES.png"),
            NodoArchivos.Archivo("INPUT.GGEZ")
            ,
            NodoArchivos.Archivo("PHYSICS.md")),
        NodoArchivos.Carpeta("ASSETS",
            NodoArchivos.Archivo("HERO.GGEZ"),
            NodoArchivos.Archivo("GROUND.jpg"),
            NodoArchivos.Archivo("RULES.txt")),
        NodoArchivos.Archivo("MAKEFILE.md"),
        NodoArchivos.Archivo("README.TXT"));
}
