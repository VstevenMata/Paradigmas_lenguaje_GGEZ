using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class PanelCentral
{
    // VARIABLES CONSTANTES
    const int AnchoScroll = 16;
    const int AltoScrollHorizontal = 16;
    const int AnchoNumeros = 34;
    const int FontSize = 14;
    const int AltoLinea = 16;
    const int AltoTabs = 24;
    const int FontSizeTabs = 12;
    const int LadoIconoTab = 16;
    const int PaddingTab = 8;
    const int EspacioIconoTexto = 4;
    const int EspacioTabs = 3;
    const int MargenArribaTabs = 4;
    const int MargenDebajoTabs = 4;

    public void Update()
    {
        ManejarClickTabs();
        ManejarEntradaTexto();

        var areaNumeros = AreaNumeros();
        var areaContenido = AreaContenido();

        scroll.Bounds = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y, AnchoScroll, areaContenido.Height);
        scroll.Update();

        scrollHorizontal.Bounds = new Rectangle(areaNumeros.X, areaContenido.Y + areaContenido.Height + 2, areaContenido.X + areaContenido.Width - areaNumeros.X, AltoScrollHorizontal);
        scrollHorizontal.Update();
    }

    public void Draw()
    {
        
        var areaNumeros = AreaNumeros();
        Raylib.DrawRectangleRec(areaNumeros, Win95Style.Background);

        
        var areaContenido = AreaContenido();
        Raylib.DrawRectangleRec(areaContenido, Color.White);
        Win95Style.DrawBevel(areaContenido, sunken: true, thickness: 3);

        var lineas = Texto.Split('\n');

        Raylib.BeginScissorMode((int)areaNumeros.X, (int)areaContenido.Y + 2, (int)(areaContenido.X + areaContenido.Width - areaNumeros.X), (int)areaContenido.Height - 4);

        for (int i = 0; i < lineas.Length; i++)
        {
            int y = (int)areaContenido.Y + 4 + i * AltoLinea;
            string numero = (i + 1).ToString("D3");
            int anchoNumero = Win95Style.MeasureText(numero, FontSize);
            Win95Style.DrawText(numero, (int)(areaNumeros.X + areaNumeros.Width - anchoNumero - 4), y, FontSize, Color.DarkGray);
            Win95Style.DrawText(lineas[i], (int)areaContenido.X + 4, y, FontSize, Color.Black);
        }

        DibujarCursor(areaContenido, lineas);

        Raylib.EndScissorMode();

          var esquinaScroll = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y + areaContenido.Height + 2, AnchoScroll, AltoScrollHorizontal);
        Raylib.DrawRectangleRec(esquinaScroll, Win95Style.Background);
        Win95Style.DrawBevel(esquinaScroll, sunken: true, thickness: 1);

        // línea que separa el texto blanco del gris de la barra de scroll vertical
        
        Win95Style.DrawDivisorVertical((int)(areaContenido.X + areaContenido.Width) + 1, (int)areaContenido.Y, (int)areaContenido.Height, thickness: 2);
        Win95Style.DrawDivisorHorizontal((int)areaContenido.X, (int)(areaContenido.Y + areaContenido.Height) + 1, (int)areaContenido.Width, thickness: 2);

        scroll.Draw();
        scrollHorizontal.Draw();

        DibujarTabs(areaContenido);
    }

    void ManejarClickTabs()
    {
        var areaTabs = AreaTabs();
        float x = areaTabs.X + 2;
        for (int i = 0; i < Archivos.Length; i++)
        {
            int ancho = AnchoDeTab(Archivos[i]);
            var tabRect = new Rectangle(x, areaTabs.Y, ancho, areaTabs.Height);
            if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), tabRect) && Raylib.IsMouseButtonPressed(MouseButton.Left))
                ArchivoActivo = i;
            x += ancho + EspacioTabs;
        }
    }

    void ManejarEntradaTexto()
    {
        int tecla = Raylib.GetCharPressed();
        while (tecla > 0)
        {
            Texto = Texto.Insert(posicionCursor, ((char)tecla).ToString());
            posicionCursor++;
            tecla = Raylib.GetCharPressed();
        }

        if (Raylib.IsKeyPressed(KeyboardKey.Enter) || Raylib.IsKeyPressedRepeat(KeyboardKey.Enter))
        {
            Texto = Texto.Insert(posicionCursor, "\n");
            posicionCursor++;
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Backspace) || Raylib.IsKeyPressedRepeat(KeyboardKey.Backspace)) && posicionCursor > 0)
        {
            Texto = Texto.Remove(posicionCursor - 1, 1);
            posicionCursor--;
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Delete) || Raylib.IsKeyPressedRepeat(KeyboardKey.Delete)) && posicionCursor < Texto.Length)
        {
            Texto = Texto.Remove(posicionCursor, 1);
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Left) || Raylib.IsKeyPressedRepeat(KeyboardKey.Left)) && posicionCursor > 0)
        {
            posicionCursor--;
        }

        if ((Raylib.IsKeyPressed(KeyboardKey.Right) || Raylib.IsKeyPressedRepeat(KeyboardKey.Right)) && posicionCursor < Texto.Length)
        {
            posicionCursor++;
        }
    }

    void DibujarTabs(Rectangle areaContenido)
    {
        var areaTabs = AreaTabs();
        float x = areaTabs.X + 2;
        for (int i = 0; i < Archivos.Length; i++)
        {
            int ancho = AnchoDeTab(Archivos[i]);
            var tabRect = new Rectangle(x, areaTabs.Y, ancho, areaTabs.Height);
            bool activa = i == ArchivoActivo;

            Raylib.DrawRectangleRec(tabRect, Win95Style.Background);

            if (activa)
            {
                Win95Style.DrawBevel(tabRect, sunken: false, thickness: 2, bordeInferior: false);

                  var conector = new Rectangle(tabRect.X + 2, tabRect.Y + tabRect.Height, tabRect.Width - 4, areaContenido.Y - (tabRect.Y + tabRect.Height) + 3);
                Raylib.DrawRectangleRec(conector, Win95Style.Background);
            }
            else
            {
                  Win95Style.DrawBevel(tabRect, sunken: false, thickness: 2);
            }

            float xContenido = tabRect.X + PaddingTab;
            if (IconoArchivo.HasValue)
            {
                var icono = IconoArchivo.Value;
                float escala = (float)LadoIconoTab / icono.Width;
                var posicionIcono = new Vector2(xContenido, tabRect.Y + (tabRect.Height - LadoIconoTab) / 2f);
                Raylib.DrawTextureEx(icono, posicionIcono, 0f, escala, Color.White);
                xContenido += LadoIconoTab + EspacioIconoTexto;
            }
            float yTexto = tabRect.Y + (tabRect.Height - FontSizeTabs) / 2f;
            Win95Style.DrawText(Archivos[i], (int)xContenido, (int)yTexto, FontSizeTabs, Color.Black);

            x += ancho + EspacioTabs;
        }
    }

    int AnchoDeTab(string nombre)
    {
        int anchoIcono = IconoArchivo.HasValue ? LadoIconoTab + EspacioIconoTexto : 0;
        return anchoIcono + Win95Style.MeasureText(nombre, FontSizeTabs) + PaddingTab * 2;
    }

    void DibujarCursor(Rectangle areaTexto, string[] lineas)
    {
        if (Raylib.GetTime() % 1.0 >= 0.5) return;

        var (linea, columna) = LineaYColumnaDelCursor(lineas);
        int x = (int)areaTexto.X + 4 + Win95Style.MeasureText(lineas[linea][..columna], FontSize);
        int y = (int)areaTexto.Y + 4 + linea * AltoLinea;
        Raylib.DrawRectangle(x, y, 1, FontSize, Color.Black);
    }

    (int linea, int columna) LineaYColumnaDelCursor(string[] lineas)
    {
        int restante = posicionCursor;
        for (int i = 0; i < lineas.Length; i++)
        {
            if (restante <= lineas[i].Length) return (i, restante);
            restante -= lineas[i].Length + 1;
        }
        return (lineas.Length - 1, lineas[^1].Length);
    }

    Rectangle AreaTabs()
    {
        return new Rectangle(Bounds.X, Bounds.Y + MargenArribaTabs, Bounds.Width, AltoTabs);
    }

    Rectangle AreaNumeros()
    {
        float y = Bounds.Y + MargenArribaTabs + AltoTabs + MargenDebajoTabs;
        float alto = Bounds.Y + Bounds.Height - y - 4 - AltoScrollHorizontal;
        return new Rectangle(Bounds.X + 2, y, AnchoNumeros, alto);
    }

    Rectangle AreaContenido()
    {
        var numeros = AreaNumeros();
        return new Rectangle(
            numeros.X + numeros.Width + 2, numeros.Y,
            Bounds.Width - 6 - AnchoScroll - AnchoNumeros - 2,
            numeros.Height);
    }

    public Rectangle Bounds;
    public string[] Archivos = { "lobby.GGEZ", "Prueba.md" };
    public int ArchivoActivo;
    public string Texto = "";
    public Texture2D? IconoArchivo;

    int posicionCursor;
    readonly ScrollBarWin95 scroll = new();
    readonly ScrollBarWin95 scrollHorizontal = new() { Horizontal = true };
}
