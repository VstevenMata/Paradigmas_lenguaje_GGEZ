using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

// Terminal de verdad: cada comando se lanza como proceso hijo del shell del sistema
// (cmd.exe en Windows, /bin/sh en Linux) y su salida se lee en segundo plano, sin trabar el render.
// El directorio actual lo lleva este panel y no el hijo, porque cada comando arranca un proceso
// nuevo y su cwd muere con el: por eso "cd" tiene que ser un comando propio.
public class PanelTerminal
{
    const int AltoTabs = 22;
    const int AnchoScroll = 16;
    const int AnchoTab = 70;
    const int MargenArribaTabs = 4;
    const int MargenDebajoTabs = 4;
    const int FontSize = 16;
    const int AltoLinea = 18;
    const int MargenTexto = 10;
    const int Espaciado = 1; // el mismo que Win95Style deja entre letras, para medir el corte de linea igual que al dibujar
    const int LineasPorRueda = 3;
    const int LimiteLineas = 2000; // recorta el historial de salida para que no crezca sin fin
    const int LimiteHistorial = 100;
    const int LineasPorCuadro = 400; // tope de salida procesada por cuadro: un comando ruidoso no debe trabar el render
    const int AnchoTabulacion = 4;
    const int EsperaLineaParcial = 150; // ms sin datos tras los cuales se muestra una linea a medio escribir
    static readonly Color ColorPrompt = Color.Lime;
    static readonly Color ColorEscrito = Color.White;
    static readonly Color ColorError = new(255, 96, 96, 255);
    static readonly Color ColorAviso = new(255, 214, 96, 255);

    public PanelTerminal()
    {
        directorioActual = Environment.CurrentDirectory;
        directorioAnterior = directorioActual;
        Escribir("Terminal GGEZ. Escriba \"help\" para ver los comandos propios y los atajos.", ColorAviso);
    }

    public void Update()
    {
        DrenarSalida();
        RevisarProcesoTerminado();
        ManejarClickTabs();

        var areaContenido = AreaContenido();
        RecalcularFilas(areaContenido);

        if (TieneFoco)
        {
            ManejarTecladoNavegacion(areaContenido);
            if (TabActiva == 0) ManejarEntrada();
        }

        // se rearma despues del teclado porque la linea viva pudo cambiar de alto al escribir
        RecalcularFilas(areaContenido);
        ManejarRueda(areaContenido);

        int filasVisibles = FilasVisibles(areaContenido);
        scroll.ProporcionThumb = Math.Clamp(filasVisibles / (float)Math.Max(1, filas.Count), 0.05f, 1f);
        scroll.Bounds = new Rectangle(areaContenido.X + areaContenido.Width + 2, areaContenido.Y, AnchoScroll, areaContenido.Height);
        scroll.Update();

        // si todo entra en pantalla no hay nada que desplazar: arrastrar la barra no debe desanclar
        if (filas.Count <= filasVisibles) scroll.Valor = 1f;

        // se recalcula despues de que el scroll pudo moverse (rueda, barra o teclas)
        ancladoAlFinal = scroll.Valor >= 0.999f;
    }

    void ManejarClickTabs()
    {
        for (int i = 0; i < Tabs.Length; i++)
        {
            var tabRect = new Rectangle(Bounds.X + i * AnchoTab, Bounds.Y + MargenArribaTabs, AnchoTab, AltoTabs);
            if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), tabRect) && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (TabActiva != i) scroll.Valor = 1f; // cada pestana se abre mostrando lo ultimo
                TabActiva = i;
            }
        }
    }

    // ─────────────────────────── entrada de teclado ───────────────────────────

    static bool Pulsada(KeyboardKey tecla) => Raylib.IsKeyPressed(tecla) || Raylib.IsKeyPressedRepeat(tecla);
    static bool CtrlAbajo => Raylib.IsKeyDown(KeyboardKey.LeftControl) || Raylib.IsKeyDown(KeyboardKey.RightControl);
    static bool ShiftAbajo => Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);

    // desplazar la salida funciona en las dos pestanas, se este escribiendo o no
    void ManejarTecladoNavegacion(Rectangle areaContenido)
    {
        int filasVisibles = FilasVisibles(areaContenido);
        if (Pulsada(KeyboardKey.PageUp)) DesplazarFilas(-filasVisibles, areaContenido);
        if (Pulsada(KeyboardKey.PageDown)) DesplazarFilas(filasVisibles, areaContenido);
        if (CtrlAbajo && Pulsada(KeyboardKey.Home)) scroll.Valor = 0f;
        if (CtrlAbajo && Pulsada(KeyboardKey.End)) scroll.Valor = 1f;
    }

    void ManejarEntrada()
    {
        // Ctrl+Shift+C copia; se revisa antes que Ctrl+C, que corta el proceso
        if (CtrlAbajo && ShiftAbajo && Pulsada(KeyboardKey.C))
        {
            Raylib.SetClipboardText(TextoCompleto());
            return;
        }

        if (CtrlAbajo && Pulsada(KeyboardKey.C)) { Interrumpir(); return; }
        // Ctrl+D en Linux, Ctrl+Z en Windows: los dos sirven, para no tener que acordarse de cual va
        if (CtrlAbajo && (Pulsada(KeyboardKey.D) || Pulsada(KeyboardKey.Z))) { CerrarEntradaEstandar(); return; }
        if (CtrlAbajo && Pulsada(KeyboardKey.L)) { LimpiarPantalla(); return; }
        if (CtrlAbajo && Pulsada(KeyboardKey.V)) { InsertarTexto(Raylib.GetClipboardText_()); return; }
        if (CtrlAbajo && Pulsada(KeyboardKey.U)) { BorrarHastaElInicio(); return; }
        if (CtrlAbajo && Pulsada(KeyboardKey.W)) { BorrarPalabraAtras(); return; }
        if (CtrlAbajo && Pulsada(KeyboardKey.A)) { MoverCursor(0); return; }
        if (CtrlAbajo && Pulsada(KeyboardKey.E)) { MoverCursor(entradaActual.Length); return; }

        if (Pulsada(KeyboardKey.Enter)) { ConfirmarLinea(); return; }
        if (Pulsada(KeyboardKey.Tab)) { Autocompletar(); return; }

        if (Pulsada(KeyboardKey.Backspace) && cursorEntrada > 0)
        {
            entradaActual = entradaActual.Remove(cursorEntrada - 1, 1);
            MoverCursor(cursorEntrada - 1);
        }

        if (Pulsada(KeyboardKey.Delete) && cursorEntrada < entradaActual.Length)
        {
            entradaActual = entradaActual.Remove(cursorEntrada, 1);
            MoverCursor(cursorEntrada);
        }

        if (Pulsada(KeyboardKey.Left)) MoverCursor(CtrlAbajo ? InicioPalabraAnterior() : cursorEntrada - 1);
        if (Pulsada(KeyboardKey.Right)) MoverCursor(CtrlAbajo ? InicioPalabraSiguiente() : cursorEntrada + 1);
        if (Pulsada(KeyboardKey.Home)) MoverCursor(0);
        if (Pulsada(KeyboardKey.End)) MoverCursor(entradaActual.Length);
        if (Pulsada(KeyboardKey.Up)) RecorrerHistorial(-1);
        if (Pulsada(KeyboardKey.Down)) RecorrerHistorial(1);

        // con Ctrl apretado las combinaciones no son texto: sin esto Ctrl+V pegaria y ademas escribiria "v"
        int tecla = Raylib.GetCharPressed();
        while (tecla > 0)
        {
            if (!CtrlAbajo) InsertarTexto(((char)tecla).ToString());
            tecla = Raylib.GetCharPressed();
        }
    }

    void MoverCursor(int posicion)
    {
        cursorEntrada = Math.Clamp(posicion, 0, entradaActual.Length);
        // al escribir siempre se vuelve al final, como en cualquier terminal
        ancladoAlFinal = true;
        scroll.Valor = 1f;
    }

    void InsertarTexto(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return;

        // un pegado multilinea se aplana: cada salto se vuelve un espacio, no dispara varios comandos de golpe
        texto = texto.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');
        entradaActual = entradaActual.Insert(cursorEntrada, texto);
        MoverCursor(cursorEntrada + texto.Length);
    }

    void BorrarHastaElInicio()
    {
        entradaActual = entradaActual[cursorEntrada..];
        MoverCursor(0);
    }

    void BorrarPalabraAtras()
    {
        int inicio = InicioPalabraAnterior();
        if (inicio == cursorEntrada) return;
        entradaActual = entradaActual.Remove(inicio, cursorEntrada - inicio);
        MoverCursor(inicio);
    }

    int InicioPalabraAnterior()
    {
        int i = cursorEntrada;
        while (i > 0 && entradaActual[i - 1] == ' ') i--;
        while (i > 0 && entradaActual[i - 1] != ' ') i--;
        return i;
    }

    int InicioPalabraSiguiente()
    {
        int i = cursorEntrada;
        while (i < entradaActual.Length && entradaActual[i] != ' ') i++;
        while (i < entradaActual.Length && entradaActual[i] == ' ') i++;
        return i;
    }

    // indiceHistorial == historial.Count significa "estoy escribiendo una linea nueva"
    void RecorrerHistorial(int direccion)
    {
        if (historial.Count == 0) return;

        if (indiceHistorial == historial.Count && direccion < 0) borrador = entradaActual;

        int nuevo = Math.Clamp(indiceHistorial + direccion, 0, historial.Count);
        if (nuevo == indiceHistorial) return;

        indiceHistorial = nuevo;
        entradaActual = indiceHistorial == historial.Count ? borrador : historial[indiceHistorial];
        MoverCursor(entradaActual.Length);
    }

    // ─────────────────────────── ejecucion de comandos ───────────────────────────

    void ConfirmarLinea()
    {
        string linea = entradaActual;
        entradaActual = "";
        MoverCursor(0);

        // con un proceso vivo la linea no es un comando: es lo que ese proceso espera por stdin
        if (procesoActivo != null)
        {
            Escribir(linea, ColorEscrito);
            try { procesoActivo.StandardInput.WriteLine(linea); }
            catch (Exception ex) { Escribir($"No se pudo enviar la entrada al proceso: {ex.Message}", ColorError); }
            return;
        }

        Escribir(linea, ColorEscrito, PromptActual());

        if (linea.Trim().Length == 0) return;

        // si el comando ya estaba en el historial se mueve al final, no se duplica
        historial.Remove(linea);
        historial.Add(linea);
        if (historial.Count > LimiteHistorial) historial.RemoveAt(0);
        indiceHistorial = historial.Count;
        borrador = "";

        if (!EjecutarComandoPropio(linea.Trim())) Lanzar(linea);
    }

    // devuelve true si el comando lo resuelve la propia terminal y no hace falta un proceso
    bool EjecutarComandoPropio(string linea)
    {
        int corte = linea.IndexOf(' ');
        string comando = (corte < 0 ? linea : linea[..corte]).ToLowerInvariant();
        string argumento = corte < 0 ? "" : linea[(corte + 1)..].Trim();

        switch (comando)
        {
            case "cd":
                CambiarDirectorio(argumento);
                return true;
            case "clear":
            case "cls":
                LimpiarPantalla();
                return true;
            case "exit":
                OnCerrar?.Invoke();
                return true;
            case "help":
                MostrarAyuda();
                return true;
            default:
                return false;
        }
    }

    void CambiarDirectorio(string argumento)
    {
        // "cd -" vuelve a la carpeta anterior, como en bash
        string destino = argumento == "-" ? directorioAnterior : ResolverRuta(argumento.Length == 0 ? "~" : argumento);

        if (!Directory.Exists(destino))
        {
            Escribir($"No existe la carpeta: {destino}", ColorError);
            return;
        }

        directorioAnterior = directorioActual;
        directorioActual = Path.GetFullPath(destino);
    }

    void MostrarAyuda()
    {
        Escribir("Comandos propios de la terminal:", ColorAviso);
        Escribir("  cd RUTA          cambia de carpeta (acepta ~, .. y rutas relativas)", ColorEscrito);
        Escribir("  cd -             vuelve a la carpeta anterior", ColorEscrito);
        Escribir("  clear, cls       limpia la pantalla", ColorEscrito);
        Escribir("  exit             cierra el panel de la terminal", ColorEscrito);
        Escribir("  help             muestra esta ayuda", ColorEscrito);
        Escribir("Cualquier otro comando lo ejecuta el shell del sistema.", ColorEscrito);
        Escribir("Atajos:", ColorAviso);
        Escribir("  Flechas izq/der   mueven el cursor; con Ctrl saltan de palabra", ColorEscrito);
        Escribir("  Flechas arr/abj   recorren el historial de comandos", ColorEscrito);
        Escribir("  Tab               autocompleta archivos y carpetas", ColorEscrito);
        Escribir("  Inicio / Fin      principio y final de la linea", ColorEscrito);
        Escribir("  Ctrl+C            cancela la linea, o corta el proceso en curso", ColorEscrito);
        Escribir("  Ctrl+D o Ctrl+Z   marca el fin de la entrada del proceso en curso", ColorEscrito);
        Escribir("  Ctrl+L            limpia la pantalla", ColorEscrito);
        Escribir("  Ctrl+U / Ctrl+W   borra la linea entera / la palabra anterior", ColorEscrito);
        Escribir("  Ctrl+V            pega del portapapeles", ColorEscrito);
        Escribir("  Ctrl+Shift+C      copia toda la salida", ColorEscrito);
        Escribir("  RePag / AvPag     desplazan la salida", ColorEscrito);
        Escribir("  Ctrl+Inicio/Fin   van al principio y al final de la salida", ColorEscrito);
    }

    void Lanzar(string comando)
    {
        var info = new ProcessStartInfo
        {
            WorkingDirectory = directorioActual,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // a proposito no se fija StandardOutputEncoding: los streams se leen en bytes y cada
        // linea se decodifica por su cuenta, porque no todas vienen en la misma codificacion
        if (OperatingSystem.IsWindows())
        {
            info.FileName = "cmd.exe";
            info.Arguments = $"/d /s /c \"{comando}\"";
        }
        else
        {
            info.FileName = "/bin/sh";
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(comando);
        }

        try
        {
            var proceso = new Process { StartInfo = info };

            streamsCerrados = 0;
            proceso.Start();
            LeerStream(proceso.StandardOutput.BaseStream, esError: false);
            LeerStream(proceso.StandardError.BaseStream, esError: true);
            procesoActivo = proceso;
        }
        catch (Exception ex)
        {
            Escribir($"No se pudo ejecutar el comando: {ex.Message}", ColorError);
        }
    }

    // corre fuera del hilo de la UI: por eso la cola es concurrente y desde aca no se tocan sus listas
    void LeerStream(Stream stream, bool esError)
    {
        _ = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            var linea = new List<byte>();

            try
            {
                var lectura = stream.ReadAsync(buffer, 0, buffer.Length);
                while (true)
                {
                    // si el proceso se queda callado a mitad de linea suele ser un prompt esperando
                    // respuesta: se muestra igual, en vez de quedar invisible hasta que el comando termine
                    if (await Task.WhenAny(lectura, Task.Delay(EsperaLineaParcial)) != lectura)
                    {
                        if (linea.Count > 0)
                        {
                            salidaPendiente.Enqueue((Decodificar(linea), esError));
                            linea.Clear();
                        }
                        continue;
                    }

                    int leidos = await lectura;
                    if (leidos == 0) break;

                    for (int i = 0; i < leidos; i++)
                    {
                        if (buffer[i] == (byte)'\n')
                        {
                            salidaPendiente.Enqueue((Decodificar(linea), esError));
                            linea.Clear();
                        }
                        else if (buffer[i] != (byte)'\r')
                        {
                            linea.Add(buffer[i]);
                        }
                    }

                    lectura = stream.ReadAsync(buffer, 0, buffer.Length);
                }
            }
            catch
            {
                // el stream se corta solo cuando se mata el proceso: no es un error que valga mostrar
            }

            if (linea.Count > 0) salidaPendiente.Enqueue((Decodificar(linea), esError));
            Interlocked.Increment(ref streamsCerrados);
        });
    }

    // en Windows conviven dos codificaciones: cmd.exe escribe sus propios mensajes en la pagina OEM
    // (850/437) mientras que casi todo lo demas ya emite UTF-8. Se prueba UTF-8 estricto primero
    // porque una secuencia UTF-8 valida practicamente no aparece por casualidad en texto OEM.
    static string Decodificar(List<byte> bytes)
    {
        var crudos = bytes.ToArray();
        try { return utf8Estricto.GetString(crudos); }
        catch (DecoderFallbackException) { return paginaOem.GetString(crudos); }
    }

    static Encoding CargarPaginaOem()
    {
        try
        {
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return Encoding.Latin1; // no deberia pasar, pero mejor texto raro que una excepcion
        }
    }

    void DrenarSalida()
    {
        for (int i = 0; i < LineasPorCuadro && salidaPendiente.TryDequeue(out var salida); i++)
        {
            string texto = Limpiar(salida.Texto);
            Escribir(texto, salida.EsError ? ColorError : ColorEscrito);
            if (salida.EsError) AgregarError(texto);
        }
    }

    // el proceso recien termino cuando salio Y ademas cerro sus dos streams: si no, todavia
    // pueden estar viajando lineas de salida por los eventos asincronos
    void RevisarProcesoTerminado()
    {
        if (procesoActivo == null || !procesoActivo.HasExited) return;
        if (Volatile.Read(ref streamsCerrados) < 2 || !salidaPendiente.IsEmpty) return;

        int codigo = procesoActivo.ExitCode;
        procesoActivo.Dispose();
        procesoActivo = null;

        // si lo corto el usuario, el codigo de salida es ruido: ya vio su propio ^C
        bool cortado = cortadoPorUsuario;
        cortadoPorUsuario = false;

        if (codigo != 0 && !cortado)
        {
            string aviso = $"[el comando termino con codigo {codigo}]";
            Escribir(aviso, ColorError);
            AgregarError(aviso);
        }
    }

    // sin esto, un programa que lee hasta el fin de la entrada (cat, findstr, sort) no terminaria nunca
    void CerrarEntradaEstandar()
    {
        if (procesoActivo == null) return;

        try { procesoActivo.StandardInput.Close(); }
        catch (Exception ex) { Escribir($"No se pudo cerrar la entrada: {ex.Message}", ColorError); }
        Escribir("^D", ColorAviso);
    }

    void Interrumpir()
    {
        if (procesoActivo != null)
        {
            // el arbol entero: el shell suele lanzar a su vez el programa real
            cortadoPorUsuario = true;
            try { procesoActivo.Kill(entireProcessTree: true); }
            catch (Exception ex) { Escribir($"No se pudo cortar el proceso: {ex.Message}", ColorError); }
            Escribir("^C", ColorAviso);
            return;
        }

        Escribir(entradaActual + "^C", ColorEscrito, PromptActual());
        entradaActual = "";
        indiceHistorial = historial.Count;
        MoverCursor(0);
    }

    // ─────────────────────────── autocompletado ───────────────────────────

    void Autocompletar()
    {
        int inicioToken = cursorEntrada == 0 ? 0 : entradaActual.LastIndexOf(' ', cursorEntrada - 1) + 1;
        string token = entradaActual[inicioToken..cursorEntrada];

        // el token puede traer carpetas adelante ("src/Pane"): solo se completa el ultimo tramo
        int corte = token.LastIndexOfAny(new[] { '/', '\\' });
        string prefijoRuta = corte < 0 ? "" : token[..(corte + 1)];
        string parcial = corte < 0 ? token : token[(corte + 1)..];

        string carpeta = ResolverRuta(prefijoRuta.Length == 0 ? "." : prefijoRuta);
        if (!Directory.Exists(carpeta)) return;

        List<string> candidatos;
        try
        {
            candidatos = Directory.EnumerateFileSystemEntries(carpeta)
                .Select(ruta => Path.GetFileName(ruta) ?? "")
                .Where(nombre => nombre.Length > 0 && nombre.StartsWith(parcial, StringComparison.OrdinalIgnoreCase))
                .OrderBy(nombre => nombre, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Escribir($"No se pudo leer la carpeta: {ex.Message}", ColorError);
            return;
        }

        if (candidatos.Count == 0) return;

        string comun = PrefijoComun(candidatos);

        // si hay varias opciones y no se puede avanzar mas, se listan, como hace bash
        if (candidatos.Count > 1 && comun.Length == parcial.Length)
        {
            Escribir(entradaActual, ColorEscrito, PromptActual());
            foreach (var nombre in candidatos) Escribir("  " + nombre, ColorAviso);
            return;
        }

        string completado = comun;
        if (candidatos.Count == 1)
        {
            // la barra final avisa que es carpeta y deja seguir bajando con otro Tab
            completado += Directory.Exists(Path.Combine(carpeta, candidatos[0])) ? Path.DirectorySeparatorChar.ToString() : " ";
        }

        string nuevoToken = prefijoRuta + completado;
        entradaActual = entradaActual[..inicioToken] + nuevoToken + entradaActual[cursorEntrada..];
        MoverCursor(inicioToken + nuevoToken.Length);
    }

    static string PrefijoComun(List<string> nombres)
    {
        string comun = nombres[0];
        foreach (var nombre in nombres)
        {
            int i = 0;
            while (i < comun.Length && i < nombre.Length && char.ToLowerInvariant(comun[i]) == char.ToLowerInvariant(nombre[i])) i++;
            comun = comun[..i];
        }
        return comun;
    }

    // ─────────────────────────── contenido y desplazamiento ───────────────────────────

    void Escribir(string texto, Color color, string prefijo = "")
    {
        foreach (var trozo in texto.Split('\n'))
        {
            lineasTerminal.Add(new Linea { Prefijo = prefijo, Texto = trozo, Color = color });
            prefijo = ""; // el prompt solo va en la primera de las lineas
        }

        if (lineasTerminal.Count > LimiteLineas)
            lineasTerminal.RemoveRange(0, lineasTerminal.Count - LimiteLineas);

        versionContenido++;
        // solo arrastra al final si el usuario ya estaba ahi: si se fue a leer mas arriba, se lo respeta
        if (ancladoAlFinal) scroll.Valor = 1f;
    }

    void AgregarError(string texto)
    {
        // la pestana arranca con un texto de relleno: se va en cuanto hay errores de verdad
        if (!huboErroresReales)
        {
            lineasErrores.Clear();
            huboErroresReales = true;
        }

        lineasErrores.Add(new Linea { Texto = texto, Color = ColorError });
        if (lineasErrores.Count > LimiteLineas)
            lineasErrores.RemoveRange(0, lineasErrores.Count - LimiteLineas);

        versionContenido++;
    }

    void LimpiarPantalla()
    {
        lineasTerminal.Clear();
        versionContenido++;
        scroll.Valor = 1f;
    }

    string TextoCompleto()
    {
        var texto = new StringBuilder();
        foreach (var linea in TabActiva == 0 ? lineasTerminal : lineasErrores)
            texto.AppendLine(linea.Prefijo + linea.Texto);
        return texto.ToString();
    }

    void ManejarRueda(Rectangle areaContenido)
    {
        if (!Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), areaContenido)) return;

        float rueda = Raylib.GetMouseWheelMoveV().Y;
        if (rueda != 0) DesplazarFilas(-(int)MathF.Round(rueda * LineasPorRueda), areaContenido);
    }

    void DesplazarFilas(int delta, Rectangle areaContenido)
    {
        int maxPrimera = Math.Max(0, filas.Count - FilasVisibles(areaContenido));
        if (maxPrimera == 0)
        {
            scroll.Valor = 1f;
            return;
        }

        int actual = (int)MathF.Round(scroll.Valor * maxPrimera);
        scroll.Valor = Math.Clamp(actual + delta, 0, maxPrimera) / (float)maxPrimera;
    }

    int FilasVisibles(Rectangle areaContenido) => Math.Max(1, (int)(areaContenido.Height - MargenTexto * 2) / AltoLinea);

    int PrimeraFilaVisible(Rectangle areaContenido)
    {
        int maxPrimera = Math.Max(0, filas.Count - FilasVisibles(areaContenido));
        return maxPrimera == 0 ? 0 : (int)MathF.Round(scroll.Valor * maxPrimera);
    }

    // ─────────────────────────── corte de lineas largas ───────────────────────────

    // se rehace solo cuando cambio algo que afecta el corte: contenido, ancho, pestana o la linea viva
    void RecalcularFilas(Rectangle areaContenido)
    {
        int ancho = (int)(areaContenido.Width - MargenTexto * 2);
        string prefijoEntrada = TabActiva == 0 ? PrefijoEntrada() : "";
        string entrada = TabActiva == 0 ? entradaActual : "";

        if (cacheVersion == versionContenido && cacheAncho == ancho && cacheTab == TabActiva
            && cacheEntrada == entrada && cachePrefijo == prefijoEntrada)
            return;

        cacheVersion = versionContenido;
        cacheAncho = ancho;
        cacheTab = TabActiva;
        cacheEntrada = entrada;
        cachePrefijo = prefijoEntrada;

        filas.Clear();
        foreach (var linea in TabActiva == 0 ? lineasTerminal : lineasErrores)
            AgregarFilas(linea.Prefijo, linea.Texto, linea.Color, ancho, esEntrada: false);

        if (TabActiva == 0) AgregarFilas(prefijoEntrada, entrada, ColorEscrito, ancho, esEntrada: true);
    }

    void AgregarFilas(string prefijo, string texto, Color color, int ancho, bool esEntrada)
    {
        float anchoPrefijo = prefijo.Length == 0 ? 0 : Win95Style.MeasureText(prefijo, FontSize);
        foreach (var (inicio, largo) in CalcularCortes(texto, ancho - anchoPrefijo, ancho))
            filas.Add(new Fila(inicio == 0 ? prefijo : "", texto.Substring(inicio, largo), color, inicio, esEntrada));
    }

    // parte el texto en tramos que entren a lo ancho, cortando por espacios cuando se puede.
    // la primera fila es mas angosta porque comparte renglon con el prompt.
    static List<(int Inicio, int Largo)> CalcularCortes(string texto, float anchoPrimera, float anchoResto)
    {
        var cortes = new List<(int, int)>();
        if (texto.Length == 0)
        {
            cortes.Add((0, 0));
            return cortes;
        }

        int i = 0;
        bool primera = true;
        while (i < texto.Length)
        {
            // el minimo evita que un panel muy angosto genere filas de un caracter (o ninguno)
            float limite = MathF.Max(primera ? anchoPrimera : anchoResto, FontSize * 4f);
            float acumulado = 0;
            int j = i;
            int ultimoEspacio = -1;

            while (j < texto.Length)
            {
                acumulado += AnchoChar(texto[j]) + Espaciado;
                if (acumulado > limite && j > i) break;
                if (texto[j] == ' ') ultimoEspacio = j;
                j++;
            }

            // el espacio se queda en la fila de arriba, asi la de abajo arranca con la palabra entera
            int fin = j >= texto.Length ? texto.Length : ultimoEspacio > i ? ultimoEspacio + 1 : j;
            cortes.Add((i, fin - i));
            i = fin;
            primera = false;
        }

        return cortes;
    }

    // la fuente no es monoespaciada, asi que se mide letra por letra; cada ancho se cachea
    static float AnchoChar(char c)
    {
        if (anchosPorLetra.TryGetValue(c, out float ancho)) return ancho;
        ancho = Win95Style.MeasureText(c.ToString(), FontSize);
        anchosPorLetra[c] = ancho;
        return ancho;
    }

    // ─────────────────────────── dibujo ───────────────────────────

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

        RecalcularFilas(areaContenido);

        int primera = PrimeraFilaVisible(areaContenido);
        int ultima = Math.Min(filas.Count, primera + FilasVisibles(areaContenido));

        Raylib.BeginScissorMode((int)areaContenido.X + 2, (int)areaContenido.Y + 2, (int)areaContenido.Width - 4, (int)areaContenido.Height - 4);
        for (int i = primera; i < ultima; i++)
        {
            var fila = filas[i];
            int y = (int)areaContenido.Y + MargenTexto + (i - primera) * AltoLinea;
            int x = (int)areaContenido.X + MargenTexto;

            if (fila.Prefijo.Length > 0)
            {
                Win95Style.DrawText(fila.Prefijo, x, y, FontSize, ColorPrompt);
                x += Win95Style.MeasureText(fila.Prefijo, FontSize);
            }

            Win95Style.DrawText(fila.Texto, x, y, FontSize, fila.Color);

            if (fila.EsEntrada) DibujarCursor(fila, i == filas.Count - 1, x, y);
        }
        Raylib.EndScissorMode();

        scroll.Draw();

        Win95Style.DrawDivisorVertical((int)(areaContenido.X + areaContenido.Width) + 1, (int)areaContenido.Y, (int)areaContenido.Height, thickness: 2);
    }

    void DibujarCursor(Fila fila, bool esUltimaFila, int xTexto, int y)
    {
        int fin = fila.Inicio + fila.Texto.Length;
        bool cursorEnEstaFila = cursorEntrada >= fila.Inicio && (cursorEntrada < fin || esUltimaFila);
        if (!cursorEnEstaFila) return;

        int offset = Math.Clamp(cursorEntrada - fila.Inicio, 0, fila.Texto.Length);
        int xCursor = xTexto + Win95Style.MeasureText(fila.Texto[..offset], FontSize);

        // barra fina y parpadeante con foco (puede caer en medio del texto); hueca y fija sin foco
        if (TieneFoco)
        {
            if (Raylib.GetTime() % 1.0 < 0.5) Raylib.DrawRectangle(xCursor, y, 2, FontSize, ColorEscrito);
        }
        else
        {
            Raylib.DrawRectangleLines(xCursor, y, 8, FontSize, ColorEscrito);
        }
    }

    Rectangle AreaContenido()
    {
        float y = Bounds.Y + MargenArribaTabs + AltoTabs + MargenDebajoTabs;
        return new Rectangle(
            Bounds.X, y,
            Bounds.Width - 4 - AnchoScroll, Bounds.Y + Bounds.Height - y);
    }

    // ─────────────────────────── prompt y rutas ───────────────────────────

    // al abrir un archivo el prompt pasa a esa carpeta, como el cwd de una terminal real
    public void RegistrarAperturaArchivo(string rutaCompleta)
    {
        string carpeta = Path.GetDirectoryName(rutaCompleta) ?? "";
        if (carpeta.Length > 0) EstablecerDirectorio(carpeta);
    }

    // se usa tambien al abrir una carpeta desde el explorador, para que la terminal quede parada ahi
    public void EstablecerDirectorio(string ruta)
    {
        if (!Directory.Exists(ruta)) return;
        directorioAnterior = directorioActual;
        directorioActual = Path.GetFullPath(ruta);
    }

    // mientras hay un proceso corriendo la linea viva es su stdin, no un comando: va sin prompt
    string PrefijoEntrada() => procesoActivo == null ? PromptActual() : "";

    // usuario@host fijos, la ruta sigue al directorio actual
    string PromptActual() => $"{Environment.UserName}@{Environment.MachineName.ToLowerInvariant()}:{RutaCorta(directorioActual)}$ ";

    // reemplaza el home real por "~"
    static string RutaCorta(string ruta)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrEmpty(home) && ruta.StartsWith(home) ? "~" + ruta[home.Length..] : ruta;
    }

    string ResolverRuta(string ruta)
    {
        ruta = ruta.Trim().Trim('"');
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (ruta.Length == 0) return directorioActual;
        if (ruta == "~") return home;
        if (ruta.StartsWith("~/") || ruta.StartsWith("~\\")) ruta = Path.Combine(home, ruta[2..]);

        try { return Path.GetFullPath(ruta, directorioActual); }
        catch { return ruta; }
    }

    // saca los codigos de color ANSI y los caracteres de control, que aca no significan nada
    static string Limpiar(string texto)
    {
        texto = secuenciasAnsi.Replace(texto, "");

        var limpio = new StringBuilder(texto.Length);
        foreach (char c in texto)
        {
            if (c == '\t') limpio.Append(' ', AnchoTabulacion);
            else if (!char.IsControl(c)) limpio.Append(c);
        }
        return limpio.ToString();
    }

    // corta el proceso que haya quedado vivo al cerrar la app, para no dejarlo huerfano
    public void Cerrar()
    {
        if (procesoActivo == null) return;
        try { procesoActivo.Kill(entireProcessTree: true); } catch { }
        procesoActivo.Dispose();
        procesoActivo = null;
    }

    public Rectangle Bounds;
    public string[] Tabs = { "Terminal", "Errores" };
    public int TabActiva;
    public bool TieneFoco; // true cuando se clickeó dentro de la terminal si no, el teclado es del editor
    public Action? OnCerrar; // lo dispara el comando "exit"

    static readonly Regex secuenciasAnsi = new("\u001b\\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);
    static readonly Dictionary<char, float> anchosPorLetra = new();
    static readonly UTF8Encoding utf8Estricto = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    static readonly Encoding paginaOem = CargarPaginaOem();

    readonly ScrollBarWin95 scroll = new() { Valor = 1f };
    readonly List<Linea> lineasTerminal = new();
    readonly List<Linea> lineasErrores = new() { new Linea { Texto = "Los errores no existen", Color = Color.White } };
    readonly List<Fila> filas = new();
    readonly List<string> historial = new();
    readonly ConcurrentQueue<(string Texto, bool EsError)> salidaPendiente = new();

    string entradaActual = ""; // lo que se va escribiendo en la línea viva, justo después del prompt
    int cursorEntrada;
    int indiceHistorial;
    string borrador = ""; // lo que se estaba escribiendo antes de empezar a recorrer el historial
    bool huboErroresReales;
    bool ancladoAlFinal = true; // false mientras el usuario se fue a mirar salida vieja
    bool cortadoPorUsuario; // true entre el Ctrl+C y el momento en que el proceso realmente muere
    string directorioActual;
    string directorioAnterior;

    Process? procesoActivo;
    int streamsCerrados; // lo tocan los hilos del pool: 2 = stdout y stderr ya cerraron

    int versionContenido;
    int cacheVersion = -1; // -1 fuerza el primer armado de filas
    int cacheAncho = -1;
    int cacheTab = -1;
    string cacheEntrada = "";
    string cachePrefijo = "";

    class Linea
    {
        public string Prefijo = "";
        public string Texto = "";
        public Color Color;
    }

    readonly record struct Fila(string Prefijo, string Texto, Color Color, int Inicio, bool EsEntrada);
}
