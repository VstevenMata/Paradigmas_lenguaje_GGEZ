using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class ExploradorArchivos
{
    const int AltoTitulo = 22;
    const int AnchoScroll = 16;
    const int AltoFila = 22;
    const int IndentacionPorNivel = 14;
    const int LadoIcono = 16;
    const int LadoIconoCarpeta = 20;
    const int LadoIconoFantasma = 32;
    const int FontSize = 12;
    const double UmbralDobleClick = 0.4;
    const int AnchoMenuContextual = 150;
    const int AltoFilaContextual = 20;
    static readonly Color ColorGuia = new(190, 190, 190, 255);

    public void Update()
    {
        var areaContenido = AreaContenido();

        int totalFilas = NodosVisibles().Count();
        int filasVisibles = Math.Max(1, (int)(areaContenido.Height / AltoFila));
        int maxPrimeraFila = Math.Max(0, totalFilas - filasVisibles);

        if (maxPrimeraFila > 0 && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), areaContenido))
        {
            float rueda = Raylib.GetMouseWheelMove();
            if (rueda != 0)
            {
                int primeraActual = PrimeraFilaVisible(areaContenido);
                int primeraNueva = Math.Clamp(primeraActual - (int)MathF.Round(rueda * 3), 0, maxPrimeraFila);
                scroll.Valor = primeraNueva / (float)maxPrimeraFila;
            }
        }

        int primeraFila = PrimeraFilaVisible(areaContenido);

        if (nodoMenuContextual != null)
        {
            ManejarMenuContextual();
        }
        else
        {
            ManejarClicks(areaContenido, primeraFila);
            ManejarClickDerecho(areaContenido, primeraFila);
        }

        scroll.Bounds = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y, AnchoScroll, areaContenido.Height);
        scroll.ProporcionThumb = Math.Clamp(filasVisibles / (float)Math.Max(1, totalFilas), 0.05f, 1f);
        scroll.Update();
    }

    public void Draw()
    {
        Win95Style.DrawText(Titulo, (int)Bounds.X + 6, (int)Bounds.Y + 4, 16, Color.Black);

        var areaContenido = AreaContenido();
        Raylib.DrawRectangleRec(areaContenido, Color.White);
        Win95Style.DrawBevel(areaContenido, sunken: true, thickness: 3);

        if (string.IsNullOrEmpty(raiz.RutaCompleta))
        {
            DibujarSinProyecto(areaContenido);
        }
        else
        {
            int primeraFila = PrimeraFilaVisible(areaContenido);

            Raylib.BeginScissorMode((int)areaContenido.X + 2, (int)areaContenido.Y + 2, (int)areaContenido.Width - 4, (int)areaContenido.Height - 4);
            DibujarNodos(areaContenido, primeraFila);
            Raylib.EndScissorMode();

            scroll.Draw();
        }

        Win95Style.DrawDivisorVertical((int)(areaContenido.X + areaContenido.Width) + 1, (int)areaContenido.Y, (int)areaContenido.Height, thickness: 2);
    }

    // sin proyecto abierto: solo el fantasma centrado, igual que el panel central sin archivos
    void DibujarSinProyecto(Rectangle areaContenido)
    {
        if (!IconoFantasma.HasValue) return;

        float escala = LadoIconoFantasma / IconoFantasma.Value.Width;
        var posicion = new Vector2(
            areaContenido.X + (areaContenido.Width - LadoIconoFantasma) / 2f,
            areaContenido.Y + (areaContenido.Height - LadoIconoFantasma) / 2f);
        Raylib.DrawTextureEx(IconoFantasma.Value, posicion, 0f, escala, Color.White);
    }

    int PrimeraFilaVisible(Rectangle areaContenido)
    {
        int totalFilas = NodosVisibles().Count();
        int filasVisibles = Math.Max(1, (int)(areaContenido.Height / AltoFila));
        int maxPrimeraFila = Math.Max(0, totalFilas - filasVisibles);
        return maxPrimeraFila == 0 ? 0 : (int)MathF.Round(scroll.Valor * maxPrimeraFila);
    }

    void DibujarNodos(Rectangle area, int primeraFila)
    {
        int fila = 0;
        DibujarNodo(raiz, 0, area, primeraFila, ref fila);
    }

    void DibujarNodo(NodoArchivos nodo, int nivel, Rectangle area, int primeraFila, ref int fila)
    {
        DibujarFila(nodo, nivel, area, fila - primeraFila);
        fila++;

        if (!nodo.EsCarpeta || !nodo.Expandido || nodo.Hijos.Count == 0) return;

        int filaInicioHijos = fila;
        foreach (var hijo in nodo.Hijos)
            DibujarNodo(hijo, nivel + 1, area, primeraFila, ref fila);

        // guía vertical conecta la carpeta con todos sus hijos visibles
        float xGuia = area.X + 4 + nivel * IndentacionPorNivel + LadoIconoCarpeta / 2f;
        float yInicio = area.Y + 2 + (filaInicioHijos - primeraFila) * AltoFila;
        float yFin = area.Y + 2 + (fila - primeraFila) * AltoFila;
        Raylib.DrawLine((int)xGuia, (int)yInicio, (int)xGuia, (int)yFin, ColorGuia);
    }

    void DibujarFila(NodoArchivos nodo, int nivel, Rectangle area, int fila)
    {
        float x = area.X + 4 + nivel * IndentacionPorNivel;
        float y = area.Y + 2 + fila * AltoFila;

        // fila seleccionada: azul marino con texto blanco, como el explorador real de Win95
        bool seleccionado = nodo == nodoSeleccionado;
        if (seleccionado)
            Raylib.DrawRectangleRec(new Rectangle(area.X, y, area.Width, AltoFila), Win95Style.TitleBar);

        var icono = IconoPara(nodo);
        if (icono.HasValue)
        {
            int lado = nodo.EsCarpeta ? LadoIconoCarpeta : LadoIcono;
            var posicionIcono = new Vector2(x, y + (AltoFila - lado) / 2f);
            Raylib.DrawTextureEx(icono.Value, posicionIcono, 0f, (float)lado / icono.Value.Width, Color.White);
        }

        Win95Style.DrawText(nodo.Nombre, (int)(x + LadoIconoCarpeta + 4), (int)(y + (AltoFila - FontSize) / 2f), FontSize, seleccionado ? Color.White : Color.Black);
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

    // un solo click selecciona (como en el explorador real de Win95); recién el doble click abre/expande
    void ManejarClicks(Rectangle area, int primeraFila)
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var mouse = Raylib.GetMousePosition();
        int fila = 0;
        foreach (var (nodo, _) in NodosVisibles())
        {
            var filaRect = new Rectangle(area.X, area.Y + 2 + (fila - primeraFila) * AltoFila, area.Width, AltoFila);
            if (Raylib.CheckCollisionPointRec(mouse, filaRect))
            {
                bool esDobleClick = nodo == nodoUltimoClick && Raylib.GetTime() - tiempoUltimoClick < UmbralDobleClick;
                nodoSeleccionado = nodo;
                nodoUltimoClick = nodo;
                tiempoUltimoClick = Raylib.GetTime();

                if (esDobleClick)
                {
                    if (nodo.EsCarpeta)
                    {
                        nodo.Expandido = !nodo.Expandido;
                        if (nodo.Expandido) nodo.Expandir();
                    }
                    else if (!string.IsNullOrEmpty(nodo.RutaCompleta))
                    {
                        OnAbrirArchivo?.Invoke(nodo);
                    }
                }
                return;
            }
            fila++;
        }

        nodoSeleccionado = null; // click en el vacío: se pierde la selección
    }

    void ManejarClickDerecho(Rectangle area, int primeraFila)
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Right) || !Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), area)) return;

        var mouse = Raylib.GetMousePosition();
        int fila = 0;
        foreach (var (nodo, _) in NodosVisibles())
        {
            var filaRect = new Rectangle(area.X, area.Y + 2 + (fila - primeraFila) * AltoFila, area.Width, AltoFila);
            if (Raylib.CheckCollisionPointRec(mouse, filaRect))
            {
                nodoSeleccionado = nodo;
                nodoMenuContextual = nodo;
                posicionMenuContextual = mouse;
                return;
            }
            fila++;
        }

        // click en el espacio vacío: si no hay proyecto abierto, ofrece arrancar uno ahí mismo
        if (string.IsNullOrEmpty(raiz.RutaCompleta))
        {
            nodoMenuContextual = raiz;
            posicionMenuContextual = mouse;
        }
    }

    // qué opciones tiene sentido ofrecer según el nodo clickeado
    static string[] OpcionesPara(NodoArchivos nodo)
    {
        if (string.IsNullOrEmpty(nodo.RutaCompleta)) return new[] { "Crear carpeta" };
        if (nodo.Padre == null) return new[] { "Nuevo archivo", "Nueva carpeta", "Cerrar carpeta", "Eliminar" }; // raíz del proyecto: no se renombra desde acá
        return new[] { "Nuevo archivo", "Nueva carpeta", "Renombrar", "Eliminar" };
    }

    void ManejarMenuContextual()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var mouse = Raylib.GetMousePosition();
        var area = RectMenuContextual();
        var opciones = OpcionesPara(nodoMenuContextual!);
        for (int i = 0; i < opciones.Length; i++)
        {
            if (Raylib.CheckCollisionPointRec(mouse, RectFilaContextual(area, i)))
            {
                EjecutarOpcionContextual(opciones[i], nodoMenuContextual!);
                break;
            }
        }

        nodoMenuContextual = null; // se cierra al elegir una opción o al clickear afuera
    }

    void EjecutarOpcionContextual(string opcion, NodoArchivos nodo)
    {
        switch (opcion)
        {
            case "Crear carpeta": CrearProyectoNuevo(); break;
            case "Nuevo archivo": CrearHijo(nodo, esCarpeta: false); break;
            case "Nueva carpeta": CrearHijo(nodo, esCarpeta: true); break;
            case "Renombrar": Renombrar(nodo); break;
            case "Cerrar carpeta": CerrarCarpeta(); break;
            case "Eliminar": Eliminar(nodo); break;
        }
    }

    // sin proyecto abierto: elige dónde crearla, le pone nombre y la abre como raíz
    void CrearProyectoNuevo()
    {
        string? ubicacion = ElegirCarpetaBase?.Invoke();
        if (string.IsNullOrEmpty(ubicacion)) return;

        string? nombre = PedirTexto?.Invoke("Crear carpeta", "Nombre de la carpeta:", "nuevo-proyecto");
        if (string.IsNullOrEmpty(nombre)) return;

        try
        {
            string ruta = Path.Combine(ubicacion, nombre);
            Directory.CreateDirectory(ruta);
            AbrirCarpeta(ruta);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo crear la carpeta: {ex.Message}");
        }
    }

    // crea como hijo de la carpeta clickeada, o como hermano si se clickeó un archivo
    void CrearHijo(NodoArchivos nodo, bool esCarpeta)
    {
        var carpetaDestino = nodo.EsCarpeta ? nodo : nodo.Padre;
        if (carpetaDestino == null || string.IsNullOrEmpty(carpetaDestino.RutaCompleta)) return;

        string valorInicial = esCarpeta ? "nueva-carpeta" : "nuevo.txt";
        string? nombre = PedirTexto?.Invoke(esCarpeta ? "Nueva carpeta" : "Nuevo archivo", "Nombre:", valorInicial);
        if (string.IsNullOrEmpty(nombre)) return;

        string ruta = Path.Combine(carpetaDestino.RutaCompleta, nombre);
        if (File.Exists(ruta) || Directory.Exists(ruta))
        {
            Dialogo?.Mostrar("Aviso", $"Ya existe \"{nombre}\" en esta carpeta.", new[] { "Cerrar" }, _ => { });
            return;
        }

        try
        {
            if (esCarpeta) Directory.CreateDirectory(ruta);
            else File.WriteAllText(ruta, "");

            carpetaDestino.Expandido = true;
            carpetaDestino.Refrescar();

            if (!esCarpeta)
            {
                var nuevoNodo = carpetaDestino.Hijos.FirstOrDefault(h => h.RutaCompleta == ruta);
                if (nuevoNodo != null) OnAbrirArchivo?.Invoke(nuevoNodo);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo crear: {ex.Message}");
        }
    }

    void Renombrar(NodoArchivos nodo)
    {
        if (nodo.Padre == null || string.IsNullOrEmpty(nodo.RutaCompleta)) return;

        string? nuevoNombre = PedirTexto?.Invoke("Renombrar", "Nuevo nombre:", nodo.Nombre);
        if (string.IsNullOrEmpty(nuevoNombre) || nuevoNombre == nodo.Nombre) return;

        try
        {
            string carpetaPadre = Path.GetDirectoryName(nodo.RutaCompleta) ?? "";
            string rutaNueva = Path.Combine(carpetaPadre, nuevoNombre);
            if (nodo.EsCarpeta) Directory.Move(nodo.RutaCompleta, rutaNueva);
            else File.Move(nodo.RutaCompleta, rutaNueva);

            string rutaVieja = nodo.RutaCompleta;
            nodo.RutaCompleta = rutaNueva;
            nodo.Nombre = nuevoNombre;
            nodo.Refrescar(); // si tenía hijos ya cargados, quedaron apuntando a la ruta vieja

            OnArchivoRenombrado?.Invoke(rutaVieja, rutaNueva, nuevoNombre);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo renombrar: {ex.Message}");
        }
    }

    void Eliminar(NodoArchivos nodo)
    {
        if (string.IsNullOrEmpty(nodo.RutaCompleta)) return;

        Dialogo?.Mostrar("Confirmar", $"Eliminar \"{nodo.Nombre}\"?", new[] { "Si", "No" }, eleccion =>
        {
            if (eleccion != 0) return;
            try
            {
                if (nodo.EsCarpeta) Directory.Delete(nodo.RutaCompleta, recursive: true);
                else File.Delete(nodo.RutaCompleta);

                if (nodo.Padre != null)
                {
                    nodo.Padre.Refrescar();
                    if (nodoSeleccionado == nodo) nodoSeleccionado = null;
                    if (nodoUltimoClick == nodo) nodoUltimoClick = null;
                }
                else
                {
                    CerrarCarpeta(); // era la raíz del proyecto: vuelve al estado "sin carpeta abierta"
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"No se pudo eliminar: {ex.Message}");
            }
        });
    }

    // aparte de Draw() a propósito: hay que llamarla al final del frame, encima de todo lo demás
    public void DrawMenuContextual()
    {
        if (nodoMenuContextual == null) return;

        var opciones = OpcionesPara(nodoMenuContextual);
        var area = RectMenuContextual();
        Raylib.DrawRectangleRec(area, Win95Style.Background);
        Win95Style.DrawBevel(area, sunken: false, thickness: 2);

        var mouse = Raylib.GetMousePosition();
        for (int i = 0; i < opciones.Length; i++)
        {
            var filaRect = RectFilaContextual(area, i);
            bool hover = Raylib.CheckCollisionPointRec(mouse, filaRect);
            if (hover) Raylib.DrawRectangleRec(filaRect, Win95Style.TitleBar);
            Win95Style.DrawText(opciones[i], (int)filaRect.X + 8, (int)filaRect.Y + 3, 14, hover ? Color.White : Color.Black);
        }
    }

    Rectangle RectMenuContextual() => new(posicionMenuContextual.X, posicionMenuContextual.Y, AnchoMenuContextual, OpcionesPara(nodoMenuContextual!).Length * AltoFilaContextual + 4);

    Rectangle RectFilaContextual(Rectangle area, int indice) => new(area.X + 2, area.Y + 2 + indice * AltoFilaContextual, area.Width - 4, AltoFilaContextual);

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

    // reemplaza la raíz por una carpeta real arranca cerrada, así el primer clic dispara el lazy load
    public void AbrirCarpeta(string ruta)
    {
        raiz = NodoArchivos.CarpetaDesdeDisco(ruta);
        nodoSeleccionado = null;
        nodoUltimoClick = null;
        nodoMenuContextual = null;
    }

    // botón de la barra lateral: si ya hay un proyecto abierto crea adentro de su raíz, si no arranca uno nuevo
    public void CrearCarpeta()
    {
        if (string.IsNullOrEmpty(raiz.RutaCompleta)) CrearProyectoNuevo();
        else CrearHijo(raiz, esCarpeta: true);
    }

    // solo deja de mostrar el proyecto en el explorador: no toca nada en disco ni cierra pestañas del editor
    public void CerrarCarpeta()
    {
        raiz = NodoArchivos.Archivo("No hay carpetas abiertas");
        nodoSeleccionado = null;
        nodoUltimoClick = null;
        nodoMenuContextual = null;
    }

    public string CarpetaRaiz => raiz.RutaCompleta;

    public Rectangle Bounds;
    public string Titulo = "Archivos";
    public Texture2D? IconoCarpetaAbierta;
    public Texture2D? IconoCarpetaCerrada;
    public Texture2D? IconoDocumento;
    public Texture2D? IconoDocumentoGgez;
    public Texture2D? IconoImagenes;
    public Texture2D? IconoDocumentoMd;
    public Texture2D? IconoFantasma; // fila placeholder cuando no hay proyecto abierto
    public Action<NodoArchivos>? OnAbrirArchivo;
    public DialogoModal? Dialogo; // confirma antes de eliminar
    public Func<string, string, string, string?>? PedirTexto; // (titulo, mensaje, valorInicial) → respuesta o null si canceló
    public Func<string?>? ElegirCarpetaBase; // para "Crear carpeta" cuando todavía no hay proyecto abierto
    public Action<string, string, string>? OnArchivoRenombrado; // (rutaVieja, rutaNueva, nombreNuevo)

    readonly ScrollBarWin95 scroll = new();

    NodoArchivos raiz = NodoArchivos.Archivo("No hay carpetas abiertas");
    NodoArchivos? nodoSeleccionado;
    NodoArchivos? nodoUltimoClick;
    double tiempoUltimoClick = -1;
    NodoArchivos? nodoMenuContextual;
    Vector2 posicionMenuContextual;
}
