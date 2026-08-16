using System.Diagnostics;
using System.Numerics;
using Raylib_cs;
using ParadigmasLenguajePropio.UI;

// VARIABLES CONSTANTES
const int anchoInicial = 1500;
const int altoInicial = 900;
const int anchoMinimo = 800;
const int altoMinimo = 450;
const int ladoBoton = 28;
const int margen = 4;
const int espacio = 3;
const int altoDivisor = 2;
const int anchoManijaResize = 6;
const int margenExplorador = 2;
const int anchoExploradorMinimo = 100;
const int anchoExploradorMaximo = 400;
const int anchoExploradorColapsado = 0; // sin franja visible al cerrar
const float velocidadColapsoExplorador = 1400f;
const float anchoPanelCentralMinimo = 200f;
const int altoTerminal = 160;
const int anchoPanelDerecho = 340;
const int espacioPanelDerecho = 8;
const int anchoFranjaBorde = 22; // franjas fijas en los bordes de la ventana
const int altoFranjaBorde = 90;

// FUNCIONES
Texture2D CargarIcono(string archivo)
{
    var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", archivo);
    var textura = Raylib.LoadTexture(ruta);
    Raylib.SetTextureFilter(textura, TextureFilter.Point);
    return textura;
}

// pantalla de arranque estilo barra segmentada mientras cargan fuente e íconos
void DibujarPantallaCarga(float progreso)
{
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.Black);

    const int anchoBarra = 320;
    const int altoBarra = 22;
    int x = (Raylib.GetScreenWidth() - anchoBarra) / 2;
    int y = (Raylib.GetScreenHeight() - altoBarra) / 2;

    Raylib.DrawText("Paradigmas GGEZ", x, y - 44, 22, Color.White);
    Raylib.DrawText("Cargando...", x, y - 20, 14, Color.LightGray);

    var fondo = new Rectangle(x, y, anchoBarra, altoBarra);
    Raylib.DrawRectangleRec(fondo, new Color(40, 40, 40, 255));
    Raylib.DrawRectangleLinesEx(fondo, 1, Color.White);

    const int segmentos = 24;
    const int margenSegmento = 2;
    float anchoSegmento = (anchoBarra - margenSegmento * (segmentos + 1)) / (float)segmentos;
    int segmentosLlenos = (int)(Math.Clamp(progreso, 0f, 1f) * segmentos);
    for (int i = 0; i < segmentosLlenos; i++)
    {
        float sx = x + margenSegmento + i * (anchoSegmento + margenSegmento);
        Raylib.DrawRectangle((int)sx, y + margenSegmento, (int)anchoSegmento, altoBarra - margenSegmento * 2, new Color(0, 0, 170, 255));
    }

    Raylib.EndDrawing();
}


// en Linux usa zenity(pero gnome, en otras distros no sé si se usará lo mismo xd)
// para no agregar dependencias de UI nativa al proyecto en sí
string? ElegirCarpeta()
{
    try
    {
        var info = OperatingSystem.IsWindows() ? InfoSelectorCarpetaWindows() : InfoSelectorCarpetaLinux();
        using var proceso = Process.Start(info);
        if (proceso == null) return null;

        string salida = proceso.StandardOutput.ReadToEnd().Trim();
        proceso.WaitForExit();
        return proceso.ExitCode == 0 && salida.Length > 0 ? salida : null;
    }
    catch (Exception ex)
    {
        Console.WriteLine($"No se pudo abrir el selector de carpetas: {ex.Message}");
        return null;
    }
}

ProcessStartInfo InfoSelectorCarpetaLinux() => new()
{
    FileName = "zenity",
    Arguments = "--file-selection --directory --title=\"Elegir una carpeta\"",
    RedirectStandardOutput = true,
    UseShellExecute = false,
};

ProcessStartInfo InfoSelectorCarpetaWindows()
{
    var info = new ProcessStartInfo
    {
        FileName = "powershell",
        RedirectStandardOutput = true,
        UseShellExecute = false,
        CreateNoWindow = true,
    };
    info.ArgumentList.Add("-NoProfile");
    info.ArgumentList.Add("-Command");
    info.ArgumentList.Add(
        "Add-Type -AssemblyName System.Windows.Forms; " +
        "$dialogo = New-Object System.Windows.Forms.FolderBrowserDialog; " +
        "$dialogo.Description = 'Elegir una carpeta'; " +
        "if ($dialogo.ShowDialog() -eq [System.Windows.Forms.DialogResult]::OK) { Write-Output $dialogo.SelectedPath }");
    return info;
}

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.UndecoratedWindow);
Raylib.InitWindow(anchoInicial, altoInicial, "Paradigmas GGEZ");
Raylib.SetWindowMinSize(anchoMinimo, altoMinimo);
Raylib.SetTargetFPS(60);

double inicioCarga = Raylib.GetTime();
DibujarPantallaCarga(0f);

Win95Style.CargarFuente(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "MSSansSerif.ttf"));

var iconoNuevo = CargarIcono("Icon_Documento.png");
var iconoGuardar = CargarIcono("Icon_NewDocumento.png");
var iconoConstruir = CargarIcono("Icon_Build.png");
var iconoPlay = CargarIcono("Icon_Play.png");
var iconoDetener = CargarIcono("Icon_Detener.png");
var iconoLupa = CargarIcono("Icon_Lupa.png");
var iconoCarpetaCerrada = CargarIcono("Icon_CapertaCerrada.png");
var iconoCarpetaAbierta = CargarIcono("Icon_CarpetaAbierta.png");
var iconoTerminal = CargarIcono("Icon_Terminal.png");
var iconoPersonaje = CargarIcono("Icon_Personaje.png");
var iconoPersonaje64px = CargarIcono("Icon_Personaje64px.png");
var iconoPuertaAbierta = CargarIcono("Icon_PuertaAbierta.png");
var iconoPuertaCerrada = CargarIcono("Icon_PuertaCerrada.png");
var iconoDocumentoGgez = CargarIcono("Icon_Documentoggez.png");
var iconoImagenes = CargarIcono("Icon_Imagenes.png");
var iconoDocumentoMd = CargarIcono("Icon_DocumentoMD.png");

// cargar los assets es casi instantáneo, pero la pantalla de arranque se mantiene un mínimo de tiempo,
const double duracionMinimaCarga = 3.5;
while (Raylib.GetTime() - inicioCarga < duracionMinimaCarga)
{
    DibujarPantallaCarga((float)((Raylib.GetTime() - inicioCarga) / duracionMinimaCarga));
}

var barraApp = new AppTitleBar("Paradigmas GGEZ");
var barraMenu = new MenuBar();

int yToolbar = AppTitleBar.Height + MenuBar.Height + altoDivisor + margen;

var botonNuevo = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoNuevo, Tooltip = "Nuevo archivo" };
var botonGuardar = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoGuardar, Tooltip = "Guardar" };
var botonConstruir = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoConstruir, Tooltip = "Construir" };
var botonPlay = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoPlay, Tooltip = "Ejecutar" };
var botonDetener = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoDetener, Tooltip = "Detener" };
var botonLupa = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoLupa, Tooltip = "Buscar" };
var botonCarpeta = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoCarpetaCerrada, Tooltip = "Abrir carpeta" };
var botonTerminal = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoTerminal, Tooltip = "Terminal" };

// grupos con sentido (archivo / ejecución / navegación), separados por una línea grabada
var gruposToolbar = new[]
{
    new[] { botonNuevo, botonGuardar },
    new[] { botonConstruir, botonPlay, botonDetener },
    new[] { botonLupa, botonCarpeta, botonTerminal },
};

var divisoresToolbar = new List<int>();
int xToolbar = margen;
foreach (var grupo in gruposToolbar)
{
    foreach (var boton in grupo)
    {
        boton.Bounds.X = xToolbar;
        xToolbar += ladoBoton + espacio;
    }
    xToolbar += espacio;
    divisoresToolbar.Add(xToolbar);
    xToolbar += espacio * 2;
}

int alturaHeader = yToolbar + ladoBoton + margen + altoDivisor;

int anchoExplorador = divisoresToolbar[^1] - margen;

// franjas fijas en el borde izquierdo/derecho de la ventana 
var botonExplorador = new Button(0, 0, anchoFranjaBorde, altoFranjaBorde, "") { Icon = iconoPuertaAbierta, Tooltip = "Mostrar/ocultar explorador" };
var botonColumnaDerecha = new Button(0, 0, anchoFranjaBorde, altoFranjaBorde, "") { Icon = iconoPuertaAbierta, Tooltip = "Mostrar/ocultar panel derecho" };

var botonesToolbar = gruposToolbar.SelectMany(g => g).Append(botonExplorador).Append(botonColumnaDerecha).ToArray();
bool arrastrandoExplorador = false;
int anchoExploradorAlIniciarArrastre = 0;
float mouseXAlIniciarArrastreExplorador = 0f;
bool exploradorOculto = false;
float anchoExploradorMostrado = anchoExplorador; // se anima hacia anchoExplorador o anchoExploradorColapsado
var explorador = new ExploradorArchivos
{
    IconoCarpetaAbierta = iconoCarpetaAbierta,
    IconoCarpetaCerrada = iconoCarpetaCerrada,
    IconoDocumento = iconoNuevo,
    IconoDocumentoGgez = iconoDocumentoGgez,
    IconoImagenes = iconoImagenes,
    IconoDocumentoMd = iconoDocumentoMd,
};
var dialogoModal = new DialogoModal();
var panelCentral = new PanelCentral
{
    IconoDocumento = iconoNuevo,
    IconoDocumentoGgez = iconoDocumentoGgez,
    IconoImagenes = iconoImagenes,
    IconoDocumentoMd = iconoDocumentoMd,
    Dialogo = dialogoModal,
};
var panelJuego = new PanelJuego(iconoPersonaje64px);
bool columnaDerechaOculta = false;
var panelInspeccion = new PanelInspeccion();


barraMenu.OnOpcionElegida = opcion =>
{
    if (opcion == "Acerca de")
        dialogoModal.Mostrar("Acerca de", "Aqui va la informacion del acerca de", new[] { "Cerrar" }, _ => { });
    if (opcion == "Sintaxis de GGEZ")
        dialogoModal.Mostrar("Sintaxis", "Aqui va la informacion sobre la sintaxis", new[] { "Cerrar" }, _ => { });
        if(opcion=="Atajos de teclado")
        dialogoModal.Mostrar("Atajos de teclado","Aqui va la informacion sobre los atajos de teclado", new[] { "Cerrar" }, _ => { });
    else
        Console.WriteLine($"Menú: {opcion}");

};

bool terminalVisible = false;
bool focoEnTerminal = false;
var panelTerminal = new PanelTerminal();

explorador.OnAbrirArchivo = nodo =>
{
    panelCentral.AbrirArchivo(nodo.Nombre, nodo.RutaCompleta);
    panelTerminal.RegistrarAperturaArchivo(nodo.RutaCompleta);
};

var barraEstado = new StatusBar();

Button? botonConTooltip = null;
double tiempoInicioHoverTooltip = 0;

while (!Raylib.WindowShouldClose() && !barraApp.ShouldClose)
{
    barraApp.Update();
    dialogoModal.Update();

    // sin archivos abiertos no hay nada que guardar, construir, correr, detener o buscar
    bool hayArchivos = panelCentral.HayArchivosAbiertos;
    botonGuardar.Habilitado = hayArchivos;
    botonConstruir.Habilitado = hayArchivos;
    botonPlay.Habilitado = hayArchivos;
    botonDetener.Habilitado = hayArchivos;
    botonLupa.Habilitado = hayArchivos;
    botonExplorador.Icon = exploradorOculto ? iconoPuertaCerrada : iconoPuertaAbierta;
    botonColumnaDerecha.Icon = columnaDerechaOculta ? iconoPuertaCerrada : iconoPuertaAbierta;

    int anchoActual = Raylib.GetScreenWidth();
    int altoActual = Raylib.GetScreenHeight();
    barraMenu.Bounds = new Rectangle(0, AppTitleBar.Height, anchoActual, MenuBar.Height);
    barraEstado.Bounds = new Rectangle(0, altoActual - StatusBar.Height, anchoActual, StatusBar.Height);
    barraEstado.PosicionCursor = panelCentral.HayArchivosAbiertos ? $"Linea {panelCentral.LineaCursor}, Columna {panelCentral.ColumnaCursor}" : "";

    int altoDisponible = altoActual - StatusBar.Height;
    int altoContenido = altoDisponible - alturaHeader - margen * 2 - (terminalVisible ? altoTerminal + margen : 0);

    // el explorador se anima hacia abierto/cerrado, igual que el juego/inspección
    float objetivoAnchoExplorador = exploradorOculto ? anchoExploradorColapsado : anchoExplorador;
    float pasoColapsoExplorador = velocidadColapsoExplorador * Raylib.GetFrameTime();
    anchoExploradorMostrado = MathF.Abs(objetivoAnchoExplorador - anchoExploradorMostrado) <= pasoColapsoExplorador
        ? objetivoAnchoExplorador
        : anchoExploradorMostrado + MathF.Sign(objetivoAnchoExplorador - anchoExploradorMostrado) * pasoColapsoExplorador;

    // las franjas de borde reservan su propio espacio 
    // centradas verticalmente en el alto del contenido, no pegadas arriba
    float yFranjaBorde = alturaHeader + margen + (altoContenido - altoFranjaBorde) / 2f;
    botonExplorador.Bounds = new Rectangle(0, yFranjaBorde, anchoFranjaBorde, altoFranjaBorde);
    botonColumnaDerecha.Bounds = new Rectangle(anchoActual - anchoFranjaBorde, yFranjaBorde, anchoFranjaBorde, altoFranjaBorde);

    explorador.Bounds = new Rectangle(anchoFranjaBorde + margenExplorador, alturaHeader + margen, anchoExploradorMostrado, altoContenido);

    int xCentral = anchoFranjaBorde + margenExplorador + (int)anchoExploradorMostrado + anchoManijaResize;
    int xPanelDerecho = anchoActual - anchoFranjaBorde - margen - anchoPanelDerecho;

    // el juego y la inspección se reparten todo el alto disponible, proporcional a lo que cada uno necesita
    int alturaColumnaDerecha = altoContenido - espacioPanelDerecho;
    int alturaJuego = alturaColumnaDerecha * panelJuego.AlturaSugerida / (panelJuego.AlturaSugerida + panelInspeccion.AlturaSugerida);
    int alturaInspeccion = alturaColumnaDerecha - alturaJuego;

    panelJuego.Bounds = new Rectangle(xPanelDerecho, alturaHeader + margen, anchoPanelDerecho, alturaJuego);
    panelInspeccion.Bounds = new Rectangle(xPanelDerecho, panelJuego.Bounds.Y + panelJuego.Bounds.Height + espacioPanelDerecho, anchoPanelDerecho, alturaInspeccion);

    // el panel central recupera el espacio de lo que esté colapsado a su derechanunca baja de un mínimo
    // así sus pestañas jamás terminan dibujándose fuera de sus propios bordes
    float xColumnaDerechaActual = Math.Min(panelJuego.BordeIzquierdoActual, panelInspeccion.BordeIzquierdoActual);
    float anchoPanelCentral = Math.Max(anchoPanelCentralMinimo, xColumnaDerechaActual - espacioPanelDerecho - xCentral);
    panelCentral.Bounds = new Rectangle(xCentral, alturaHeader + margen, anchoPanelCentral, altoContenido);

    if (terminalVisible)
        panelTerminal.Bounds = new Rectangle(margen, explorador.Bounds.Y + explorador.Bounds.Height + margen, anchoActual - margen * 2, altoTerminal);
    else
        focoEnTerminal = false; // si la terminal está cerrada, el teclado siempre es del editor

    var manijaResizeExplorador = new Rectangle(explorador.Bounds.X + explorador.Bounds.Width, explorador.Bounds.Y, anchoManijaResize, explorador.Bounds.Height);
    bool mouseSobreManija = !exploradorOculto && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), manijaResizeExplorador);

    // con el diálogo modal abierto, el resto de la app queda congelada no procesa clicks ni teclado
    if (!dialogoModal.Visible)
    {
        // si el menú ya estaba abierto, este click es para elegir una opción o cerrarlo no debe
        // pasar a lo que esté dibujado debajo del desplegable 
        bool clickParaElMenu = barraMenu.MenuAbierto && Raylib.IsMouseButtonPressed(MouseButton.Left);
        barraMenu.Update();

        if (!clickParaElMenu)
        {
            if (botonNuevo.IsClicked()) Console.WriteLine("Nuevo documento");
            if (botonGuardar.IsClicked()) Console.WriteLine("Guardar documento");
            if (botonConstruir.IsClicked()) Console.WriteLine("Construir");
            if (botonPlay.IsClicked()) Console.WriteLine("Correr");
            if (botonDetener.IsClicked()) Console.WriteLine("Detener");
            if (botonLupa.IsClicked()) Console.WriteLine("Buscar");
            if (botonCarpeta.IsClicked())
            {
                var rutaElegida = ElegirCarpeta();
                if (rutaElegida != null)
                {
                    explorador.AbrirCarpeta(rutaElegida);
                    botonCarpeta.Icon = iconoCarpetaAbierta;
                    Console.WriteLine($"Carpeta abierta: {rutaElegida}");
                }
            }
            if (botonTerminal.IsClicked())
            {
                terminalVisible = !terminalVisible;
                Console.WriteLine(terminalVisible ? "Abrir terminal" : "Cerrar terminal");
            }
            if (botonExplorador.IsClicked()) exploradorOculto = !exploradorOculto;
            if (botonColumnaDerecha.IsClicked()) columnaDerechaOculta = !columnaDerechaOculta;

            if (!exploradorOculto) explorador.Update();

            panelJuego.Oculto = columnaDerechaOculta;
            panelInspeccion.Oculto = columnaDerechaOculta;
            panelJuego.Update();
            panelInspeccion.Update();

            // el panel donde se hace click se queda con el foco del teclado el otro se congela sin perder lo escrito
            if (Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                if (Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), panelCentral.Bounds)) focoEnTerminal = false;
                else if (terminalVisible && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), panelTerminal.Bounds)) focoEnTerminal = true;
            }

            panelCentral.TieneFoco = !focoEnTerminal;
            panelCentral.Update();

            if (terminalVisible)
            {
                panelTerminal.TieneFoco = focoEnTerminal;
                panelTerminal.Update();
            }

            // hueco entre el explorador y el contenido de al lado se puede arrastrar
            if (!arrastrandoExplorador && mouseSobreManija && Raylib.IsMouseButtonPressed(MouseButton.Left))
            {
                arrastrandoExplorador = true;
                anchoExploradorAlIniciarArrastre = anchoExplorador;
                mouseXAlIniciarArrastreExplorador = Raylib.GetMousePosition().X;
            }
            if (arrastrandoExplorador && !Raylib.IsMouseButtonDown(MouseButton.Left))
            {
                arrastrandoExplorador = false;
            }
            if (arrastrandoExplorador)
            {
                float deltaX = Raylib.GetMousePosition().X - mouseXAlIniciarArrastreExplorador;
                anchoExplorador = Math.Clamp(anchoExploradorAlIniciarArrastre + (int)deltaX, anchoExploradorMinimo, anchoExploradorMaximo);
            }
            Raylib.SetMouseCursor(mouseSobreManija || arrastrandoExplorador ? MouseCursor.ResizeEw : MouseCursor.Default);

            // tooltip estilo Win95: aparece tras un ratito quieto sobre un botón, se cierra apenas cambia el hover
            var mouse = Raylib.GetMousePosition();
            Button? botonHover = Array.Find(botonesToolbar, b => b.Tooltip != null && Raylib.CheckCollisionPointRec(mouse, b.Bounds));
            if (botonHover != botonConTooltip)
            {
                botonConTooltip = botonHover;
                tiempoInicioHoverTooltip = Raylib.GetTime();
            }
        }
    }
    else
    {
        Raylib.SetMouseCursor(MouseCursor.Default);
        botonConTooltip = null;
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Win95Style.Background);

    barraMenu.Draw();
    Win95Style.DrawDivisorHorizontal(0, AppTitleBar.Height + MenuBar.Height, anchoActual);

    foreach (var boton in botonesToolbar)
    {
        boton.Draw();
    }
    foreach (var xDivisor in divisoresToolbar)
    {
        Win95Style.DrawDivisorVertical(xDivisor, yToolbar, ladoBoton);
    }
    Win95Style.DrawDivisorHorizontal(0, alturaHeader - altoDivisor, anchoActual);

    if (exploradorOculto)
        Raylib.DrawRectangleRec(explorador.Bounds, Win95Style.Background);
    else
        explorador.Draw();

    // línea que separa el explorador del centro
    int xDivisorCentro = (int)(explorador.Bounds.X + explorador.Bounds.Width + anchoManijaResize / 2);
    Win95Style.DrawDivisorVertical(xDivisorCentro, (int)explorador.Bounds.Y, (int)explorador.Bounds.Height);

    panelCentral.Draw();

    // línea que separa el centro de la columna derecha
    int xDivisorDerecho = (int)(panelCentral.Bounds.X + panelCentral.Bounds.Width + espacioPanelDerecho / 2);
    Win95Style.DrawDivisorVertical(xDivisorDerecho, (int)panelCentral.Bounds.Y, (int)panelCentral.Bounds.Height);

    panelJuego.Draw();
    panelInspeccion.Draw();

    if (terminalVisible)
    {
        // línea que separa la terminal del contenido de arriba, en el medio del hueco
        int yDivisorTerminal = (int)(explorador.Bounds.Y + explorador.Bounds.Height + margen / 2);
        Win95Style.DrawDivisorHorizontal(0, yDivisorTerminal, anchoActual);

        panelTerminal.Draw();
    }

    barraEstado.Draw();
    barraApp.Draw();

    // al final de todo, así quedan encima del explorador/editor/terminal
    barraMenu.DrawMenuAbierto();
    explorador.DrawMenuContextual();

    const double retrasoTooltip = 0.5;
    if (botonConTooltip?.Tooltip != null && Raylib.GetTime() - tiempoInicioHoverTooltip > retrasoTooltip)
    {
        // si el botón está en la mitad derecha de la pantalla, el tooltip cuelga hacia la izquierda
        
        bool enMitadDerecha = botonConTooltip.Bounds.X + botonConTooltip.Bounds.Width / 2f > anchoActual / 2f;
        int anchoTooltip = Win95Style.MeasureText(botonConTooltip.Tooltip, 14) + 8;
        int xTooltip = enMitadDerecha
            ? (int)(botonConTooltip.Bounds.X + botonConTooltip.Bounds.Width) - anchoTooltip
            : (int)botonConTooltip.Bounds.X;
        Win95Style.DrawTooltip(botonConTooltip.Tooltip, xTooltip, (int)(botonConTooltip.Bounds.Y + botonConTooltip.Bounds.Height + 4));
    }

    // el diálogo modal es lo último de todo bloquea visualmente el resto de la ventana
    dialogoModal.Draw();

    Raylib.EndDrawing();
}

Raylib.UnloadTexture(iconoNuevo);
Raylib.UnloadTexture(iconoGuardar);
Raylib.UnloadTexture(iconoConstruir);
Raylib.UnloadTexture(iconoPlay);
Raylib.UnloadTexture(iconoDetener);
Raylib.UnloadTexture(iconoLupa);
Raylib.UnloadTexture(iconoCarpetaCerrada);
Raylib.UnloadTexture(iconoCarpetaAbierta);
Raylib.UnloadTexture(iconoTerminal);
Raylib.UnloadTexture(iconoPersonaje);
Raylib.UnloadTexture(iconoPuertaAbierta);
Raylib.UnloadTexture(iconoPuertaCerrada);
Raylib.UnloadTexture(iconoDocumentoGgez);
Raylib.UnloadTexture(iconoImagenes);
Raylib.UnloadTexture(iconoDocumentoMd);
Raylib.UnloadFont(Win95Style.Fuente);
Raylib.CloseWindow();