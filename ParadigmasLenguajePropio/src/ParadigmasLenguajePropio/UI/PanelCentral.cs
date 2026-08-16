using System.Numerics;
using System.Text;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class PanelCentral
{
    // VARIABLES CONSTANTES
    const int AnchoScroll = 16;
    const int AltoScrollHorizontal = 16;
    const int AnchoNumeros = 40;
    const int FontSize = 18;
    const int AltoLinea = 20;
    const int AltoTabs = 24;
    const int FontSizeTabs = 16;
    const int LadoIconoTab = 16;
    const int PaddingTab = 8;
    const int EspacioIconoTexto = 4;
    const int EspacioTabs = 3;
    const int LadoBotonCerrar = 14;
    const int EspacioBotonCerrar = 6;
    const int MargenArribaTabs = 4;
    const int MargenDebajoTabs = 4;
    const string EspaciosTab = "    ";
    const int LimiteHistorial = 200;
    const int AnchoMaximoTab = 130;
    const int LineasPorRueda = 3;
    const float PixelesPorRuedaTabs = 40f;
    const float PixelesPorRuedaHorizontal = 40f;
    static readonly Color ColorSeleccion = new(51, 153, 255, 110);
    static readonly Color ColorLineaActual = new(235, 235, 235, 255);

    public void Update()
    {
        ManejarClickTabs();
        ManejarRuedaTabs();
        if (archivos.Count == 0) return; // nada que editar ni scrollear sin pestañas abiertas

        var areaNumeros = AreaNumeros();
        var areaContenido = AreaContenido();

        int filaAntes = filaCursor;
        int columnaAntes = columnaCursor;
        ManejarMouseTexto(areaContenido);
        // solo escribe si el editor tiene el foco se hizo click acá y no en la terminal
        if (TieneFoco) ManejarTeclado();
        // solo se pone el cursor si realmente se movió, así la rueda del mouse puede alejarse de él libremente
        if (filaCursor != filaAntes || columnaCursor != columnaAntes)
            AsegurarCursorVisible(areaContenido);

        ManejarRuedaTexto(areaNumeros, areaContenido);

        int lineasVisibles = Math.Max(1, (int)(areaContenido.Height / AltoLinea));
        scroll.ProporcionThumb = Math.Clamp(lineasVisibles / (float)Math.Max(1, lineas.Count), 0.05f, 1f);
        scroll.Bounds = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y, AnchoScroll, areaContenido.Height);
        scroll.Update();

        int anchoTotal = AnchoLineaMasLarga();
        scrollHorizontal.ProporcionThumb = Math.Clamp(areaContenido.Width / (float)Math.Max(1, anchoTotal), 0.05f, 1f);
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

        if (archivos.Count == 0)
        {
            DibujarSinArchivos(areaContenido);
            DibujarTabs(areaContenido);
            return;
        }

        DibujarTexto(areaNumeros, areaContenido);

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

    void DibujarSinArchivos(Rectangle areaContenido)
    {
        const string mensaje = "Sin archivos abiertos";
        int ancho = Win95Style.MeasureText(mensaje, FontSize);
        int x = (int)(areaContenido.X + (areaContenido.Width - ancho) / 2);
        int y = (int)(areaContenido.Y + (areaContenido.Height - FontSize) / 2);
        Win95Style.DrawText(mensaje, x, y, FontSize, Color.Gray);
    }

    void ManejarClickTabs()
    {
        if (!Raylib.IsMouseButtonPressed(MouseButton.Left)) return;

        var areaTabs = AreaTabs();
        var mouse = Raylib.GetMousePosition();
        if (!Raylib.CheckCollisionPointRec(mouse, areaTabs)) return;

        float x = areaTabs.X + 2 - desplazamientoTabs;
        for (int i = 0; i < archivos.Count; i++)
        {
            int ancho = AnchoDeTab(archivos[i]);
            var tabRect = new Rectangle(x, areaTabs.Y, ancho, areaTabs.Height);

            if (Raylib.CheckCollisionPointRec(mouse, RectBotonCerrar(tabRect)))
            {
                CerrarArchivo(i);
                return;
            }
            if (Raylib.CheckCollisionPointRec(mouse, tabRect) && i != ArchivoActivo)
            {
                CambiarArchivoActivo(i);
                return;
            }
            x += ancho + EspacioTabs;
        }
    }

    // rueda del mouse sobre la barra de pestañas las desliza cuando no caben todas en el ancho del panel
    void ManejarRuedaTabs()
    {
        var areaTabs = AreaTabs();
        if (!Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), areaTabs)) return;

        float rueda = Raylib.GetMouseWheelMove();
        if (rueda == 0) return;

        float maxDesplazamiento = Math.Max(0, AnchoTotalTabs() - areaTabs.Width);
        desplazamientoTabs = Math.Clamp(desplazamientoTabs - rueda * PixelesPorRuedaTabs, 0, maxDesplazamiento);
    }

    // corrige el desplazamiento para que la pestaña activa siempre quede visible dentro del panel
    void AsegurarTabVisible()
    {
        var areaTabs = AreaTabs();
        if (archivos.Count == 0) { desplazamientoTabs = 0; return; }

        float xTab = XAcumuladoHasta(ArchivoActivo);
        float anchoTab = AnchoDeTab(archivos[ArchivoActivo]);

        if (xTab < desplazamientoTabs) desplazamientoTabs = xTab;
        else if (xTab + anchoTab > desplazamientoTabs + areaTabs.Width) desplazamientoTabs = xTab + anchoTab - areaTabs.Width;

        float maxDesplazamiento = Math.Max(0, AnchoTotalTabs() - areaTabs.Width);
        desplazamientoTabs = Math.Clamp(desplazamientoTabs, 0, maxDesplazamiento);
    }

    float XAcumuladoHasta(int indice)
    {
        float x = 0;
        for (int i = 0; i < indice; i++)
            x += AnchoDeTab(archivos[i]) + EspacioTabs;
        return x;
    }

    float AnchoTotalTabs()
    {
        float total = 0;
        for (int i = 0; i < archivos.Count; i++)
            total += AnchoDeTab(archivos[i]) + EspacioTabs;
        return total;
    }

    // si hay cambios sin guardar pregunta antes de cerrar; si no, cierra directo
    void CerrarArchivo(int indice)
    {
        if (archivos[indice].Modificado && Dialogo != null)
        {
            string nombre = archivos[indice].Nombre;
            Dialogo.Mostrar("Confirmar", $"Cerrar \"{nombre}\" sin guardar los cambios?", new[] { "Si", "No" }, eleccion =>
            {
                if (eleccion == 0) CerrarArchivoInterno(indice);
            });
            return;
        }
        CerrarArchivoInterno(indice);
    }

    // cierra la pestaña si era la activa, pasa a la que quede en su lugar sin resetear el cursor de las demás
    void CerrarArchivoInterno(int indice)
    {
        archivos.RemoveAt(indice);

        if (archivos.Count == 0)
        {
            ArchivoActivo = 0;
        }
        else if (indice == ArchivoActivo)
        {
            CambiarArchivoActivo(Math.Min(indice, archivos.Count - 1));
        }
        else if (indice < ArchivoActivo)
        {
            ArchivoActivo--;
        }
        AsegurarTabVisible();
    }

    // reemplaza a la pestaña si el archivo ya está abierto si no, crea una nueva y carga su contenido
    public void AbrirArchivo(string nombre, string rutaCompleta)
    {
        int existente = archivos.FindIndex(a => a.RutaCompleta == rutaCompleta);
        if (existente >= 0)
        {
            if (existente != ArchivoActivo) CambiarArchivoActivo(existente);
            return;
        }

        List<string> contenido;
        try
        {
            string texto = File.ReadAllText(rutaCompleta);
            contenido = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n').ToList();
            if (contenido.Count == 0) contenido.Add("");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo abrir {rutaCompleta}: {ex.Message}");
            return;
        }

        archivos.Add(new ArchivoAbierto { Nombre = nombre, RutaCompleta = rutaCompleta, Lineas = contenido, Icono = IconoPara(nombre) });
        CambiarArchivoActivo(archivos.Count - 1);
    }

    void CambiarArchivoActivo(int indice)
    {
        ArchivoActivo = indice;
        filaCursor = 0;
        columnaCursor = 0;
        columnaDeseada = 0;
        haySeleccion = false;
        scroll.Valor = 0;
        scrollHorizontal.Valor = 0;
        AsegurarTabVisible();
    }

    // trunca los nombres de pestaña muy largos, en vez de dejar que la pestaña crezca sin límite
    string NombreMostrado(string nombre)
    {
        if (Win95Style.MeasureText(nombre, FontSizeTabs) <= AnchoMaximoTab) return nombre;

        string truncado = nombre;
        while (truncado.Length > 1 && Win95Style.MeasureText(truncado + "...", FontSizeTabs) > AnchoMaximoTab)
            truncado = truncado[..^1];
        return truncado + "...";
    }

    void ManejarMouseTexto(Rectangle areaContenido)
    {
        var mouse = Raylib.GetMousePosition();

        if (Raylib.CheckCollisionPointRec(mouse, areaContenido) && Raylib.IsMouseButtonPressed(MouseButton.Left))
        {
            var (fila, columna) = PosicionEnPixel(areaContenido, mouse);
            MoverCursor(fila, columna, extenderSeleccion: false);
            columnaDeseada = columna;
            arrastrandoSeleccion = true;
        }
        else if (arrastrandoSeleccion && Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            var (fila, columna) = PosicionEnPixel(areaContenido, mouse);
            MoverCursor(fila, columna, extenderSeleccion: true);
            columnaDeseada = columna;
        }

        if (!Raylib.IsMouseButtonDown(MouseButton.Left)) arrastrandoSeleccion = false;
    }

    (int fila, int columna) PosicionEnPixel(Rectangle areaContenido, Vector2 mouse)
    {
        int primeraLinea = PrimeraLineaVisible(areaContenido);
        float desplazH = DesplazamientoHorizontal(areaContenido);

        int fila = (int)((mouse.Y - areaContenido.Y - 4) / AltoLinea) + primeraLinea;
        fila = Math.Clamp(fila, 0, lineas.Count - 1);

        float xRelativo = mouse.X - (areaContenido.X + 4 - desplazH);
        int columna = ColumnaEnX(lineas[fila], xRelativo);
        return (fila, columna);
    }

    int ColumnaEnX(string linea, float xRelativo)
    {
        int anchoPrevio = 0;
        for (int c = 0; c < linea.Length; c++)
        {
            int anchoActual = Win95Style.MeasureText(linea[..(c + 1)], FontSize);
            if (xRelativo < (anchoPrevio + anchoActual) / 2f) return c;
            anchoPrevio = anchoActual;
        }
        return linea.Length;
    }

    void ManejarTeclado()
    {
        bool ctrl = Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        bool alt = Raylib.IsKeyDown(KeyboardKey.LeftAlt) || Raylib.IsKeyDown(KeyboardKey.RightAlt);

        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.C)) { Copiar(); return; }
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.X)) { Cortar(); return; }
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.V)) { Pegar(); return; }
        if (ctrl && Raylib.IsKeyPressed(KeyboardKey.A)) { SeleccionarTodo(); return; }
        if (ctrl && Pulsado(KeyboardKey.Z)) { Deshacer(); return; }

        if (alt && Pulsado(KeyboardKey.Up)) { MoverLineaArriba(); return; }
        if (alt && Pulsado(KeyboardKey.Down)) { MoverLineaAbajo(); return; }

        if (!ctrl)
        {
            int tecla = Raylib.GetCharPressed();
            while (tecla > 0)
            {
                InsertarTexto(((char)tecla).ToString());
                tecla = Raylib.GetCharPressed();
            }
        }

        if (Pulsado(KeyboardKey.Enter)) InsertarNuevaLinea();
        if (Pulsado(KeyboardKey.Tab)) InsertarTexto(EspaciosTab);
        if (Pulsado(KeyboardKey.Backspace)) Retroceder();
        if (Pulsado(KeyboardKey.Delete)) Suprimir();

        if (Pulsado(KeyboardKey.Left)) MoverIzquierda(shift);
        else if (Pulsado(KeyboardKey.Right)) MoverDerecha(shift);
        else if (Pulsado(KeyboardKey.Up)) MoverArriba(shift);
        else if (Pulsado(KeyboardKey.Down)) MoverAbajo(shift);
        else if (Pulsado(KeyboardKey.Home)) MoverInicioLinea(shift);
        else if (Pulsado(KeyboardKey.End)) MoverFinLinea(shift);
    }

    static bool Pulsado(KeyboardKey tecla) => Raylib.IsKeyPressed(tecla) || Raylib.IsKeyPressedRepeat(tecla);

    //  edición 

    void InsertarTexto(string texto)
    {
        bool esUnCaracter = texto.Length == 1;
        GuardarEstadoParaDeshacer(esUnCaracter ? TipoEdicion.Escritura : TipoEdicion.Otro);

        if (haySeleccion) BorrarSeleccion();

        var partes = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string antes = lineas[filaCursor][..columnaCursor];
        string despues = lineas[filaCursor][columnaCursor..];

        if (partes.Length == 1)
        {
            lineas[filaCursor] = antes + partes[0] + despues;
            columnaCursor += partes[0].Length;
        }
        else
        {
            lineas[filaCursor] = antes + partes[0];
            for (int i = 1; i < partes.Length - 1; i++)
                lineas.Insert(filaCursor + i, partes[i]);
            lineas.Insert(filaCursor + partes.Length - 1, partes[^1] + despues);
            filaCursor += partes.Length - 1;
            columnaCursor = partes[^1].Length;
        }
        columnaDeseada = columnaCursor;
    }

    void InsertarNuevaLinea()
    {
        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        if (haySeleccion) BorrarSeleccion();

        string antes = lineas[filaCursor][..columnaCursor];
        string despues = lineas[filaCursor][columnaCursor..];
        lineas[filaCursor] = antes;
        lineas.Insert(filaCursor + 1, despues);
        filaCursor++;
        columnaCursor = 0;
        columnaDeseada = 0;
    }

    void Retroceder()
    {
        if (haySeleccion)
        {
            GuardarEstadoParaDeshacer(TipoEdicion.Otro);
            BorrarSeleccion();
            return;
        }

        GuardarEstadoParaDeshacer(TipoEdicion.Borrado);
        if (columnaCursor > 0)
        {
            lineas[filaCursor] = lineas[filaCursor].Remove(columnaCursor - 1, 1);
            columnaCursor--;
        }
        else if (filaCursor > 0)
        {
            int columnaUnion = lineas[filaCursor - 1].Length;
            lineas[filaCursor - 1] += lineas[filaCursor];
            lineas.RemoveAt(filaCursor);
            filaCursor--;
            columnaCursor = columnaUnion;
        }
        columnaDeseada = columnaCursor;
    }

    void Suprimir()
    {
        if (haySeleccion)
        {
            GuardarEstadoParaDeshacer(TipoEdicion.Otro);
            BorrarSeleccion();
            return;
        }

        GuardarEstadoParaDeshacer(TipoEdicion.Borrado);
        if (columnaCursor < lineas[filaCursor].Length)
        {
            lineas[filaCursor] = lineas[filaCursor].Remove(columnaCursor, 1);
        }
        else if (filaCursor < lineas.Count - 1)
        {
            lineas[filaCursor] += lineas[filaCursor + 1];
            lineas.RemoveAt(filaCursor + 1);
        }
    }

    void MoverLineaArriba()
    {
        if (filaCursor == 0) return;
        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        (lineas[filaCursor - 1], lineas[filaCursor]) = (lineas[filaCursor], lineas[filaCursor - 1]);
        filaCursor--;
    }

    void MoverLineaAbajo()
    {
        if (filaCursor == lineas.Count - 1) return;
        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        (lineas[filaCursor + 1], lineas[filaCursor]) = (lineas[filaCursor], lineas[filaCursor + 1]);
        filaCursor++;
    }

    // portapapeles

    void Copiar()
    {
        if (haySeleccion) Raylib.SetClipboardText(TextoSeleccionado());
    }

    void Cortar()
    {
        if (!haySeleccion) return;
        Raylib.SetClipboardText(TextoSeleccionado());
        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        BorrarSeleccion();
    }

    void Pegar()
    {
        string texto = Raylib.GetClipboardText_();
        if (!string.IsNullOrEmpty(texto)) InsertarTexto(texto);
    }

    void SeleccionarTodo()
    {
        selFila = 0;
        selColumna = 0;
        haySeleccion = true;
        filaCursor = lineas.Count - 1;
        columnaCursor = lineas[filaCursor].Length;
        columnaDeseada = columnaCursor;
    }

    // navegación

    void MoverCursor(int fila, int columna, bool extenderSeleccion)
    {
        fila = Math.Clamp(fila, 0, lineas.Count - 1);
        columna = Math.Clamp(columna, 0, lineas[fila].Length);

        if (extenderSeleccion)
        {
            if (!haySeleccion)
            {
                haySeleccion = true;
                selFila = filaCursor;
                selColumna = columnaCursor;
            }
        }
        else
        {
            haySeleccion = false;
        }

        filaCursor = fila;
        columnaCursor = columna;
    }

    void MoverIzquierda(bool extender)
    {
        if (columnaCursor > 0) MoverCursor(filaCursor, columnaCursor - 1, extender);
        else if (filaCursor > 0) MoverCursor(filaCursor - 1, lineas[filaCursor - 1].Length, extender);
        columnaDeseada = columnaCursor;
    }

    void MoverDerecha(bool extender)
    {
        if (columnaCursor < lineas[filaCursor].Length) MoverCursor(filaCursor, columnaCursor + 1, extender);
        else if (filaCursor < lineas.Count - 1) MoverCursor(filaCursor + 1, 0, extender);
        columnaDeseada = columnaCursor;
    }

    void MoverArriba(bool extender)
    {
        if (filaCursor == 0) { MoverCursor(0, 0, extender); columnaDeseada = 0; return; }
        MoverCursor(filaCursor - 1, Math.Min(columnaDeseada, lineas[filaCursor - 1].Length), extender);
    }

    void MoverAbajo(bool extender)
    {
        if (filaCursor == lineas.Count - 1) { MoverCursor(filaCursor, lineas[filaCursor].Length, extender); columnaDeseada = columnaCursor; return; }
        MoverCursor(filaCursor + 1, Math.Min(columnaDeseada, lineas[filaCursor + 1].Length), extender);
    }

    void MoverInicioLinea(bool extender)
    {
        MoverCursor(filaCursor, 0, extender);
        columnaDeseada = 0;
    }

    void MoverFinLinea(bool extender)
    {
        MoverCursor(filaCursor, lineas[filaCursor].Length, extender);
        columnaDeseada = columnaCursor;
    }

    // deshacer

    void GuardarEstadoParaDeshacer(TipoEdicion tipo)
    {
        // escritura y borrado se agrupan mientras se repiten, así deshacer no va letra por letra
        archivos[ArchivoActivo].Modificado = true;

        bool seAgrupaConLaAnterior = (tipo == TipoEdicion.Escritura || tipo == TipoEdicion.Borrado) && tipo == ultimoTipoEdicion;
        if (seAgrupaConLaAnterior) return;

        historialDeshacer.Add((new List<string>(lineas), filaCursor, columnaCursor));
        if (historialDeshacer.Count > LimiteHistorial) historialDeshacer.RemoveAt(0);
        ultimoTipoEdicion = tipo;
    }

    void Deshacer()
    {
        if (historialDeshacer.Count == 0) return;

        var (lineasPrevias, fila, columna) = historialDeshacer[^1];
        historialDeshacer.RemoveAt(historialDeshacer.Count - 1);

        lineas.Clear();
        lineas.AddRange(lineasPrevias);
        filaCursor = Math.Clamp(fila, 0, lineas.Count - 1);
        columnaCursor = Math.Clamp(columna, 0, lineas[filaCursor].Length);
        columnaDeseada = columnaCursor;
        haySeleccion = false;
        ultimoTipoEdicion = TipoEdicion.Ninguna; // la próxima edición tiene su propio grupo
    }

    enum TipoEdicion { Ninguna, Escritura, Borrado, Otro }

    // selección

    bool AnclaEsAntesDelCursor() => selFila != filaCursor ? selFila < filaCursor : selColumna < columnaCursor;

    (int fila, int columna) SeleccionDesde() => AnclaEsAntesDelCursor() ? (selFila, selColumna) : (filaCursor, columnaCursor);
    (int fila, int columna) SeleccionHasta() => AnclaEsAntesDelCursor() ? (filaCursor, columnaCursor) : (selFila, selColumna);

    string TextoSeleccionado()
    {
        if (!haySeleccion) return "";
        var (fi, ci) = SeleccionDesde();
        var (ff, cf) = SeleccionHasta();
        if (fi == ff) return lineas[fi][ci..cf];

        var sb = new StringBuilder();
        sb.Append(lineas[fi][ci..]);
        for (int f = fi + 1; f < ff; f++)
        {
            sb.Append('\n');
            sb.Append(lineas[f]);
        }
        sb.Append('\n');
        sb.Append(lineas[ff][..cf]);
        return sb.ToString();
    }

    void BorrarSeleccion()
    {
        if (!haySeleccion) return;
        var (fi, ci) = SeleccionDesde();
        var (ff, cf) = SeleccionHasta();

        if (fi == ff)
        {
            lineas[fi] = lineas[fi].Remove(ci, cf - ci);
        }
        else
        {
            lineas[fi] = lineas[fi][..ci] + lineas[ff][cf..];
            lineas.RemoveRange(fi + 1, ff - fi);
        }

        filaCursor = fi;
        columnaCursor = ci;
        columnaDeseada = ci;
        haySeleccion = false;
    }

    // scroll

    // rueda del mouse o gesto del touchpad sobre el editorbaja/sube por el texto sin mover el cursor
    void ManejarRuedaTexto(Rectangle areaNumeros, Rectangle areaContenido)
    {
        var areaEditor = new Rectangle(areaNumeros.X, areaContenido.Y, areaContenido.X + areaContenido.Width - areaNumeros.X, areaContenido.Height);
        if (!Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), areaEditor)) return;

        // GetMouseWheelMoveV trae X e Y por separado el touchpad puede mover ambos ejes a la vez
        var rueda = Raylib.GetMouseWheelMoveV();
        if (rueda.Y != 0)
        {
            int lineasVisibles = Math.Max(1, (int)(areaContenido.Height / AltoLinea));
            int maxPrimeraLinea = Math.Max(0, lineas.Count - lineasVisibles);
            if (maxPrimeraLinea > 0)
            {
                int primeraActual = (int)MathF.Round(scroll.Valor * maxPrimeraLinea);
                int primeraNueva = Math.Clamp(primeraActual - (int)MathF.Round(rueda.Y * LineasPorRueda), 0, maxPrimeraLinea);
                scroll.Valor = primeraNueva / (float)maxPrimeraLinea;
            }
        }

        if (rueda.X != 0)
        {
            int maxDesplazamiento = Math.Max(0, AnchoLineaMasLarga() - (int)areaContenido.Width);
            if (maxDesplazamiento > 0)
            {
                int actual = (int)(scrollHorizontal.Valor * maxDesplazamiento);
                int nuevo = Math.Clamp(actual + (int)MathF.Round(rueda.X * PixelesPorRuedaHorizontal), 0, maxDesplazamiento);
                scrollHorizontal.Valor = nuevo / (float)maxDesplazamiento;
            }
        }
    }

    int PrimeraLineaVisible(Rectangle areaContenido)
    {
        int lineasVisibles = Math.Max(1, (int)(areaContenido.Height / AltoLinea));
        int maxPrimeraLinea = Math.Max(0, lineas.Count - lineasVisibles);
        return maxPrimeraLinea == 0 ? 0 : (int)MathF.Round(scroll.Valor * maxPrimeraLinea);
    }

    float DesplazamientoHorizontal(Rectangle areaContenido)
    {
        int maxDesplazamiento = Math.Max(0, AnchoLineaMasLarga() - (int)areaContenido.Width);
        return maxDesplazamiento == 0 ? 0 : scrollHorizontal.Valor * maxDesplazamiento;
    }

    int AnchoLineaMasLarga()
    {
        int max = 0;
        foreach (var linea in lineas)
            max = Math.Max(max, Win95Style.MeasureText(linea, FontSize));
        return max;
    }

    void AsegurarCursorVisible(Rectangle areaContenido)
    {
        int lineasVisibles = Math.Max(1, (int)(areaContenido.Height / AltoLinea));
        int maxPrimeraLinea = Math.Max(0, lineas.Count - lineasVisibles);
        if (maxPrimeraLinea > 0)
        {
            int primeraActual = (int)MathF.Round(scroll.Valor * maxPrimeraLinea);
            int primeraDeseada = Math.Clamp(filaCursor < primeraActual ? filaCursor : filaCursor >= primeraActual + lineasVisibles ? filaCursor - lineasVisibles + 1 : primeraActual, 0, maxPrimeraLinea);
            scroll.Valor = primeraDeseada / (float)maxPrimeraLinea;
        }
        else
        {
            scroll.Valor = 0;
        }

        int maxDesplazamiento = Math.Max(0, AnchoLineaMasLarga() - (int)areaContenido.Width);
        if (maxDesplazamiento > 0)
        {
            int xCursor = Win95Style.MeasureText(lineas[filaCursor][..columnaCursor], FontSize);
            int actual = (int)(scrollHorizontal.Valor * maxDesplazamiento);
            int deseado = actual;
            if (xCursor < actual) deseado = xCursor;
            else if (xCursor > actual + areaContenido.Width - 24) deseado = (int)(xCursor - areaContenido.Width + 24);
            deseado = Math.Clamp(deseado, 0, maxDesplazamiento);
            scrollHorizontal.Valor = deseado / (float)maxDesplazamiento;
        }
        else
        {
            scrollHorizontal.Valor = 0;
        }
    }

    // dibujo del texto

    void DibujarTexto(Rectangle areaNumeros, Rectangle areaContenido)
    {
        int primeraLinea = PrimeraLineaVisible(areaContenido);
        float desplazH = DesplazamientoHorizontal(areaContenido);
        int lineasVisibles = Math.Max(1, (int)(areaContenido.Height / AltoLinea));
        int ultimaLinea = Math.Min(lineas.Count - 1, primeraLinea + lineasVisibles);

     
        Raylib.BeginScissorMode((int)areaContenido.X, (int)areaContenido.Y + 2, (int)areaContenido.Width, (int)areaContenido.Height - 4);
        for (int i = primeraLinea; i <= ultimaLinea; i++)
        {
            int y = (int)areaContenido.Y + 4 + (i - primeraLinea) * AltoLinea;

            // franja sutil sobre la línea donde está el cursor, como en cualquier editor moderno;
            // mismo Y/alto que el rectángulo de selección, para que coincidan si la línea tiene ambas cosas.
            // recortada 3px de cada lado para no tapar el bevel hundido del borde del panel
            if (i == filaCursor)
                Raylib.DrawRectangleRec(new Rectangle(areaContenido.X + 3, y, areaContenido.Width - 6, AltoLinea), ColorLineaActual);

            DibujarSeleccionEnLinea(i, areaContenido, y, desplazH);
            Win95Style.DrawText(lineas[i], (int)(areaContenido.X + 4 - desplazH), y, FontSize, Color.Black);
        }
        DibujarCursor(areaContenido, primeraLinea, desplazH);
        Raylib.EndScissorMode();

        // los números de línea quedan encima de todo y fijos, sin desplazarse con el scroll horizontal
        for (int i = primeraLinea; i <= ultimaLinea; i++)
        {
            int y = (int)areaContenido.Y + 4 + (i - primeraLinea) * AltoLinea;
            string numero = (i + 1).ToString("D3");
            int anchoNumero = Win95Style.MeasureText(numero, FontSize);
            Win95Style.DrawText(numero, (int)(areaNumeros.X + areaNumeros.Width - anchoNumero - 4), y, FontSize, Color.DarkGray);
        }
    }

    void DibujarSeleccionEnLinea(int indiceLinea, Rectangle areaContenido, int y, float desplazH)
    {
        if (!haySeleccion) return;
        var (fi, ci) = SeleccionDesde();
        var (ff, cf) = SeleccionHasta();
        if (indiceLinea < fi || indiceLinea > ff) return;

        string linea = lineas[indiceLinea];
        int columnaInicio = indiceLinea == fi ? ci : 0;
        int columnaFin = indiceLinea == ff ? cf : linea.Length;

        int xInicio = Win95Style.MeasureText(linea[..columnaInicio], FontSize);
        int xFin = Win95Style.MeasureText(linea[..columnaFin], FontSize);
        if (indiceLinea != ff) xFin += 6; // se nota que la selección sigue en la próxima línea

        var rect = new Rectangle(areaContenido.X + 4 - desplazH + xInicio, y, Math.Max(2, xFin - xInicio), AltoLinea);
        Raylib.DrawRectangleRec(rect, ColorSeleccion);
    }

    void DibujarCursor(Rectangle areaContenido, int primeraLinea, float desplazH)
    {
        if (!TieneFoco) return; // sin foco no se muestra, como al pasar a la terminal 
        if (Raylib.GetTime() % 1.0 >= 0.5) return;
        if (filaCursor < primeraLinea) return;

        int x = (int)(areaContenido.X + 4 - desplazH) + Win95Style.MeasureText(lineas[filaCursor][..columnaCursor], FontSize);
        int y = (int)areaContenido.Y + 4 + (filaCursor - primeraLinea) * AltoLinea;
        Raylib.DrawRectangle(x, y, 2, FontSize, Color.Black);
    }

    // pestañas

    void DibujarTabs(Rectangle areaContenido)
    {
        var areaTabs = AreaTabs();

        // recorta la franja de pestañas al ancho del panel, así una pestaña de más no se sale de su rango
        var recorte = new Rectangle(Bounds.X, areaTabs.Y, Bounds.Width, areaContenido.Y + 3 - areaTabs.Y);
        Raylib.BeginScissorMode((int)recorte.X, (int)recorte.Y, (int)recorte.Width, (int)recorte.Height);

        float x = areaTabs.X + 2 - desplazamientoTabs;
        for (int i = 0; i < archivos.Count; i++)
        {
            string nombreMostrado = NombreMostrado(archivos[i].Nombre);
            int ancho = AnchoDeTab(archivos[i]);
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

            // punto de "modificado" a la izquierda, pegado al documento, antes del ícono
            if (archivos[i].Modificado)
                Raylib.DrawCircleV(new Vector2(tabRect.X + 5, tabRect.Y + tabRect.Height / 2f), 2, Color.Black);

            var iconoTab = archivos[i].Icono;
            if (iconoTab.HasValue)
            {
                var icono = iconoTab.Value;
                float escala = (float)LadoIconoTab / icono.Width;
                var posicionIcono = new Vector2(xContenido, tabRect.Y + (tabRect.Height - LadoIconoTab) / 2f);
                Raylib.DrawTextureEx(icono, posicionIcono, 0f, escala, Color.White);
                xContenido += LadoIconoTab + EspacioIconoTexto;
            }
            float yTexto = tabRect.Y + (tabRect.Height - FontSizeTabs) / 2f;
            Win95Style.DrawText(nombreMostrado, (int)xContenido, (int)yTexto, FontSizeTabs, Color.Black);

            var rectCerrar = RectBotonCerrar(tabRect);
            bool sobreCerrar = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), rectCerrar);
            if (sobreCerrar) Raylib.DrawRectangleRec(rectCerrar, new Color(200, 60, 60, 255));
            Color colorX = sobreCerrar ? Color.White : Color.Black;
            const int padX = 3;
            Raylib.DrawLine((int)rectCerrar.X + padX, (int)rectCerrar.Y + padX, (int)(rectCerrar.X + rectCerrar.Width) - padX, (int)(rectCerrar.Y + rectCerrar.Height) - padX, colorX);
            Raylib.DrawLine((int)(rectCerrar.X + rectCerrar.Width) - padX, (int)rectCerrar.Y + padX, (int)rectCerrar.X + padX, (int)(rectCerrar.Y + rectCerrar.Height) - padX, colorX);

            x += ancho + EspacioTabs;
        }

        Raylib.EndScissorMode();
    }

    // botón x pegado al borde derecho de la pestaña
    Rectangle RectBotonCerrar(Rectangle tabRect)
    {
        float y = tabRect.Y + (tabRect.Height - LadoBotonCerrar) / 2f;
        return new Rectangle(tabRect.X + tabRect.Width - PaddingTab - LadoBotonCerrar, y, LadoBotonCerrar, LadoBotonCerrar);
    }

    int AnchoDeTab(ArchivoAbierto archivo)
    {
        int anchoIcono = archivo.Icono.HasValue ? LadoIconoTab + EspacioIconoTexto : 0;
        return anchoIcono + Win95Style.MeasureText(NombreMostrado(archivo.Nombre), FontSizeTabs) + PaddingTab * 2 + EspacioBotonCerrar + LadoBotonCerrar;
    }

    // mismo criterio por extensión que usa el explorador, así la pestaña y el árbol muestran el mismo ícono
    Texture2D? IconoPara(string nombre)
    {
        if (nombre.EndsWith(".ggez", StringComparison.OrdinalIgnoreCase)) return IconoDocumentoGgez;
        if (EsImagen(nombre)) return IconoImagenes;
        if (nombre.EndsWith(".md", StringComparison.OrdinalIgnoreCase)) return IconoDocumentoMd;
        return IconoDocumento;
    }

    static bool EsImagen(string nombre)
    {
        string[] extensiones = { ".png", ".jpg", ".jpeg", ".bmp", ".gif" };
        return extensiones.Any(ext => nombre.EndsWith(ext, StringComparison.OrdinalIgnoreCase));
    }

    // áreas

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
    public bool TieneFoco = true; // el editor arranca con el foco del teclado se cede al hacer click en la terminal
    public int ArchivoActivo;
    public Texture2D? IconoDocumento;
    public Texture2D? IconoDocumentoGgez;
    public Texture2D? IconoImagenes;
    public Texture2D? IconoDocumentoMd;
    public DialogoModal? Dialogo; // si está asignado, confirma antes de cerrar una pestaña con cambios sin guardar
    public string Texto => archivos.Count > 0 ? string.Join('\n', lineas) : "";
    public bool HayArchivosAbiertos => archivos.Count > 0;
    public int LineaCursor => filaCursor + 1;
    public int ColumnaCursor => columnaCursor + 1;

    int filaCursor;
    int columnaCursor;
    int columnaDeseada;
    bool haySeleccion;
    int selFila;
    int selColumna;
    bool arrastrandoSeleccion;

    readonly ScrollBarWin95 scroll = new();
    readonly ScrollBarWin95 scrollHorizontal = new() { Horizontal = true };
    float desplazamientoTabs; // en píxeles, cuánto se deslizó la franja de pestañas hacia la derecha

    // cada pestaña guarda su propio contenido e historial de deshacer, así no se mezclan entre archivos
    List<string> lineas => archivos[ArchivoActivo].Lineas;
    List<(List<string> Lineas, int Fila, int Columna)> historialDeshacer => archivos[ArchivoActivo].HistorialDeshacer;

    TipoEdicion ultimoTipoEdicion
    {
        get => archivos[ArchivoActivo].UltimoTipoEdicion;
        set => archivos[ArchivoActivo].UltimoTipoEdicion = value;
    }

    class ArchivoAbierto
    {
        public string Nombre = "";
        public string RutaCompleta = "";
        public List<string> Lineas = new() { "" };
        public List<(List<string> Lineas, int Fila, int Columna)> HistorialDeshacer = new();
        public TipoEdicion UltimoTipoEdicion = TipoEdicion.Ninguna;
        public Texture2D? Icono;
        public bool Modificado;
    }

    // arranca sin pestañas se van agregando al abrir archivos desde el explorador
    readonly List<ArchivoAbierto> archivos = new();
}
