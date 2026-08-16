using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class MenuBar
{
    // VARIABLES CONSTANTES
    public const int Height = 24;
    const int PaddingItem = 18;
    const int AnchoSubmenu = 190;
    const int AltoFilaSubmenu = 22;
    const int PaddingFilaSubmenu = 10;
    const int FontSizeSubmenu = 16;
    const string Separador = "-";

    public void Update()
    {
        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) menuAbierto = -1;

        // mnemonics: Alt + letra subrayada abre ese menú, sin necesidad del mouse
        bool alt = Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);
        if (alt)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (Raylib.IsKeyPressed((KeyboardKey)(int)Mnemonico(i)))
                {
                    menuAbierto = menuAbierto == i ? -1 : i;
                    return;
                }
            }
        }

        var mouse = Raylib.GetMousePosition();
        var rects = RectsItems();

        // con un menú ya abierto, pasar el mouse sobre otro encabezado lo abre a ese
        if (menuAbierto >= 0)
        {
            for (int i = 0; i < rects.Length; i++)
            {
                if (i != menuAbierto && Raylib.CheckCollisionPointRec(mouse, rects[i]))
                {
                    menuAbierto = i;
                    return;
                }
            }
        }

        for (int i = 0; i < rects.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, rects[i]) && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                menuAbierto = menuAbierto == i ? -1 : i;
                return;
            }
        }

        if (menuAbierto < 0 || !Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var opciones = Submenus[menuAbierto];
        var areaSubmenu = RectSubmenu(rects[menuAbierto], opciones.Length);

        for (int i = 0; i < opciones.Length; i++)
        {
            if (opciones[i] == Separador) continue;

            var filaRect = RectFilaSubmenu(areaSubmenu, i);
            if (Raylib.CheckCollisionPointRec(mouse, filaRect))
            {
                OnOpcionElegida?.Invoke(opciones[i]);
                menuAbierto = -1;
                return;
            }
        }

        // click afuera del submenú se cierra
        if (!Raylib.CheckCollisionPointRec(mouse, areaSubmenu))
            menuAbierto = -1;
    }

    public void Draw()
    {
        Raylib.DrawRectangleRec(Bounds, Win95Style.Background);

        var rects = RectsItems();
        for (int i = 0; i < Items.Length; i++)
        {
            bool activo = i == menuAbierto;
            if (activo)
            {
                // hundido y sin borde inferior: queda egado al submenú que se abre debajo
                Win95Style.DrawBevel(rects[i], sunken: true, thickness: 1, bordeInferior: false);
            }
            int xTexto = (int)rects[i].X + 6;
            int yTexto = (int)Bounds.Y + 4;
            Win95Style.DrawText(Items[i], xTexto, yTexto, 16, Color.Black);

            // subraya la letra mnemónica (Alt+letra), como en el Windows clásico
            int indiceMnemonico = MnemonicIndices[i];
            int xAntes = Win95Style.MeasureText(Items[i][..indiceMnemonico], 16);
            int anchoLetra = Win95Style.MeasureText(Items[i][indiceMnemonico].ToString(), 16);
            int yLinea = yTexto + 16;
            Raylib.DrawLine(xTexto + xAntes, yLinea, xTexto + xAntes + anchoLetra, yLinea, Color.Black);
        }
    }

    char Mnemonico(int indice) => char.ToUpperInvariant(Items[indice][MnemonicIndices[indice]]);

   public void DrawMenuAbierto()
    {
        if (menuAbierto < 0) return;
        DibujarSubmenu(RectsItems()[menuAbierto]);
    }

    void DibujarSubmenu(Rectangle rectItem)
    {
        var opciones = Submenus[menuAbierto];
        var area = RectSubmenu(rectItem, opciones.Length);
        var mouse = Raylib.GetMousePosition();

        Raylib.DrawRectangleRec(area, Win95Style.Background);
        Win95Style.DrawBevel(area, sunken: false, thickness: 2);

        for (int i = 0; i < opciones.Length; i++)
        {
            var filaRect = RectFilaSubmenu(area, i);
            string texto = opciones[i];

            if (texto == Separador)
            {
                float yLinea = filaRect.Y + filaRect.Height / 2f;
                Win95Style.DrawDivisorHorizontal((int)filaRect.X + 2, (int)yLinea, (int)filaRect.Width - 4);
                continue;
            }

            bool hover = Raylib.CheckCollisionPointRec(mouse, filaRect);
            if (hover) Raylib.DrawRectangleRec(filaRect, Win95Style.TitleBar);

            Win95Style.DrawText(texto, (int)filaRect.X + PaddingFilaSubmenu, (int)filaRect.Y + 3, FontSizeSubmenu, hover ? Color.White : Color.Black);
        }
    }

        Rectangle[] RectsItems()
    {
        var resultado = new Rectangle[Items.Length];
        int x = (int)Bounds.X + 6;
        for (int i = 0; i < Items.Length; i++)
        {
            int anchoTexto = Win95Style.MeasureText(Items[i], 16);
            resultado[i] = new Rectangle(x - 6, Bounds.Y, anchoTexto + 12, Bounds.Height);
            x += anchoTexto + PaddingItem;
        }
        return resultado;
    }

    Rectangle RectSubmenu(Rectangle rectItem, int cantidadOpciones)
    {
        return new Rectangle(rectItem.X, rectItem.Y + rectItem.Height, AnchoSubmenu, cantidadOpciones * AltoFilaSubmenu + 4);
    }

    Rectangle RectFilaSubmenu(Rectangle area, int indice)
    {
        return new Rectangle(area.X + 2, area.Y + 2 + indice * AltoFilaSubmenu, area.Width - 4, AltoFilaSubmenu);
    }

    public string[] Items = { "Archivo", "Editar", "Ejecutar", "Ayuda" };

    // índice de la letra subrayada por ítem: Archivo=A, Editar=E, eJecutar=J (la E ya la usa Editar), aYuda=Y (la A ya la usa Archivo)
    public int[] MnemonicIndices = { 0, 0, 1, 1 };

    // cada encabezado con sus acciones
    public string[][] Submenus =
    {
        new[] { "Nuevo", "Abrir carpeta", Separador, "Guardar", "Guardar como", "Guardar todo", Separador, "Salir" },
        new[] { "Deshacer", "Rehacer", Separador, "Cortar", "Copiar", "Pegar", Separador, "Buscar", "Buscar y reemplazar" },
        new[] { "Construir", "Ejecutar", "Pausar / Reanudar", "Detener", Separador, "Ver salida" },
        new[] { "Acerca de", "Atajos de teclado", "Sintaxis de GGEZ" },
    };

    public Rectangle Bounds;
    public Action<string>? OnOpcionElegida;
    public bool MenuAbierto => menuAbierto >= 0;

    int menuAbierto = -1;
}
