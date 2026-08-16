using System.Diagnostics;
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
const int altoTerminal = 160;
const int anchoPanelDerecho = 340;
const int espacioPanelDerecho = 8;

// FUNCIONES
Texture2D CargarIcono(string archivo)
{
    var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", archivo);
    var textura = Raylib.LoadTexture(ruta);
    Raylib.SetTextureFilter(textura, TextureFilter.Point);
    return textura;
}

// abre el selector de carpetas nativo del escritorio (zenity) null si se canceló o no está disponible
string? ElegirCarpeta()
{
    try
    {
        var info = new ProcessStartInfo
        {
            FileName = "zenity",
            Arguments = "--file-selection --directory --title=\"Elegir una carpeta\"",
            RedirectStandardOutput = true,
            UseShellExecute = false,
        };
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

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.UndecoratedWindow);
Raylib.InitWindow(anchoInicial, altoInicial, "Paradigmas GGEZ");
Raylib.SetWindowMinSize(anchoMinimo, altoMinimo);
Raylib.SetTargetFPS(60);

Win95Style.CargarFuente(Path.Combine(AppContext.BaseDirectory, "Assets", "Fonts", "MSSansSerif.ttf"));

var barraApp = new AppTitleBar("Paradigmas GGEZ");
var barraMenu = new MenuBar();

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

int yToolbar = AppTitleBar.Height + MenuBar.Height + altoDivisor + margen;

var botonNuevo = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoNuevo };
var botonGuardar = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoGuardar };
var botonConstruir = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoConstruir };
var botonPlay = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoPlay };
var botonDetener = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoDetener };
var botonLupa = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoLupa };
var botonCarpeta = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoCarpetaCerrada };
var botonTerminal = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoTerminal };

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
var botonesToolbar = gruposToolbar.SelectMany(g => g).ToArray();

int anchoExplorador = divisoresToolbar[^1] - margen;
bool arrastrandoExplorador = false;
int anchoExploradorAlIniciarArrastre = 0;
float mouseXAlIniciarArrastreExplorador = 0f;
var explorador = new ExploradorArchivos
{
    IconoCarpetaAbierta = iconoCarpetaAbierta,
    IconoCarpetaCerrada = iconoCarpetaCerrada,
    IconoDocumento = iconoNuevo,
    IconoDocumentoGgez = iconoDocumentoGgez,
    IconoImagenes = iconoImagenes,
    IconoDocumentoMd = iconoDocumentoMd,
};
var panelCentral = new PanelCentral
{
    IconoDocumento = iconoNuevo,
    IconoDocumentoGgez = iconoDocumentoGgez,
    IconoImagenes = iconoImagenes,
    IconoDocumentoMd = iconoDocumentoMd,
};
var panelJuego = new PanelJuego(iconoPersonaje64px, iconoPuertaAbierta, iconoPuertaCerrada);
var panelInspeccion = new PanelInspeccion(iconoPuertaAbierta, iconoPuertaCerrada);

bool terminalVisible = false;
bool focoEnTerminal = false;
var panelTerminal = new PanelTerminal();

explorador.OnAbrirArchivo = nodo =>
{
    panelCentral.AbrirArchivo(nodo.Nombre, nodo.RutaCompleta);
    panelTerminal.RegistrarAperturaArchivo(nodo.RutaCompleta);
};

var barraEstado = new StatusBar();

while (!Raylib.WindowShouldClose() && !barraApp.ShouldClose)
{
    barraApp.Update();

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

    int anchoActual = Raylib.GetScreenWidth();
    int altoActual = Raylib.GetScreenHeight();
    barraMenu.Bounds = new Rectangle(0, AppTitleBar.Height, anchoActual, MenuBar.Height);
    barraMenu.Update();
    barraEstado.Bounds = new Rectangle(0, altoActual - StatusBar.Height, anchoActual, StatusBar.Height);

    int altoDisponible = altoActual - StatusBar.Height;
    int altoContenido = altoDisponible - alturaHeader - margen * 2 - (terminalVisible ? altoTerminal + margen : 0);
    explorador.Bounds = new Rectangle(margenExplorador, alturaHeader + margen, anchoExplorador, altoContenido);
    explorador.Update();

    int xCentral = margenExplorador + anchoExplorador + anchoManijaResize;
    int xPanelDerecho = anchoActual - margen - anchoPanelDerecho;

    // el juego y la inspección se reparten todo el alto disponible, proporcional a lo que cada uno necesita
    int alturaColumnaDerecha = altoContenido - espacioPanelDerecho;
    int alturaJuego = alturaColumnaDerecha * panelJuego.AlturaSugerida / (panelJuego.AlturaSugerida + panelInspeccion.AlturaSugerida);
    int alturaInspeccion = alturaColumnaDerecha - alturaJuego;

    panelJuego.Bounds = new Rectangle(xPanelDerecho, alturaHeader + margen, anchoPanelDerecho, alturaJuego);
    panelJuego.Update();

    panelInspeccion.Bounds = new Rectangle(xPanelDerecho, panelJuego.Bounds.Y + panelJuego.Bounds.Height + espacioPanelDerecho, anchoPanelDerecho, alturaInspeccion);
    panelInspeccion.Update();

    // el panel central recupera el espacio de lo que esté colapsado a su derecha
    float xColumnaDerechaActual = Math.Min(panelJuego.BordeIzquierdoActual, panelInspeccion.BordeIzquierdoActual);
    panelCentral.Bounds = new Rectangle(xCentral, alturaHeader + margen, xColumnaDerechaActual - espacioPanelDerecho - xCentral, altoContenido);

    if (terminalVisible)
        panelTerminal.Bounds = new Rectangle(margen, explorador.Bounds.Y + explorador.Bounds.Height + margen, anchoActual - margen * 2, altoTerminal);
    else
        focoEnTerminal = false; // si la terminal está cerrada, el teclado siempre es del editor

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
    var manijaResizeExplorador = new Rectangle(explorador.Bounds.X + explorador.Bounds.Width, explorador.Bounds.Y, anchoManijaResize, explorador.Bounds.Height);
    bool mouseSobreManija = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), manijaResizeExplorador);

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

    // al final de todo, así el desplegable del menú queda encima del explorador/editor/terminal
    barraMenu.DrawMenuAbierto();

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