using Raylib_cs;
using ParadigmasLenguajePropio.UI;

const int anchoInicial = 1500;
const int altoInicial = 900;
const int anchoMinimo = 800;
const int altoMinimo = 450;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow | ConfigFlags.UndecoratedWindow);
Raylib.InitWindow(anchoInicial, altoInicial, "Paradigmas GGEZ");
Raylib.SetWindowMinSize(anchoMinimo, altoMinimo);
Raylib.SetTargetFPS(60);

var barraApp = new AppTitleBar("Paradigmas GGEZ");
var barraMenu = new MenuBar();

Texture2D CargarIcono(string archivo)
{
    var ruta = Path.Combine(AppContext.BaseDirectory, "Assets", "Icons", archivo);
    var textura = Raylib.LoadTexture(ruta);
    Raylib.SetTextureFilter(textura, TextureFilter.Point);
    return textura;
}

var iconoNuevo = CargarIcono("Icon_Documento.png");
var iconoGuardar = CargarIcono("Icon_NewDocumento.png");
var iconoPlay = CargarIcono("Icon_Play.png");
var iconoPausa = CargarIcono("Icon_Pausa.png");
var iconoLupa = CargarIcono("Icon_Lupa.png");
var iconoCarpetaCerrada = CargarIcono("Icon_CapertaCerrada.png");
var iconoCarpetaAbierta = CargarIcono("Icon_CarpetaAbierta.png");
var iconoTerminal = CargarIcono("Icon_Terminal.png");

const int ladoBoton = 24;
const int margen = 4;
const int espacio = 3;
const int altoDivisor = 2;

int yToolbar = AppTitleBar.Height + MenuBar.Height + altoDivisor + margen;

var botonNuevo = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoNuevo };
var botonGuardar = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoGuardar };
var botonPlay = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoPlay };
var botonPausa = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoPausa };
var botonLupa = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoLupa };
var botonCarpeta = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoCarpetaCerrada };
var botonTerminal = new Button(0, yToolbar, ladoBoton, ladoBoton, "") { Icon = iconoTerminal };

// grupos con sentido (archivo / ejecución / navegación), separados por una línea grabada
var gruposToolbar = new[]
{
    new[] { botonNuevo, botonGuardar },
    new[] { botonPlay, botonPausa },
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
bool carpetaAbierta = false;

int anchoExplorador = 150;
const int anchoManijaResize = 6;
const int anchoExploradorMinimo = 100;
const int anchoExploradorMaximo = 400;
bool arrastrandoExplorador = false;
int anchoExploradorAlIniciarArrastre = 0;
float mouseXAlIniciarArrastreExplorador = 0f;
var explorador = new ExploradorArchivos();

while (!Raylib.WindowShouldClose() && !barraApp.ShouldClose)
{
    barraApp.Update();

    if (botonNuevo.IsClicked()) Console.WriteLine("Nuevo documento");
    if (botonGuardar.IsClicked()) Console.WriteLine("Guardar documento");
    if (botonPlay.IsClicked()) Console.WriteLine("Correr");
    if (botonPausa.IsClicked()) Console.WriteLine("Pausar");
    if (botonLupa.IsClicked()) Console.WriteLine("Buscar");
    if (botonCarpeta.IsClicked())
    {
        carpetaAbierta = !carpetaAbierta;
        botonCarpeta.Icon = carpetaAbierta ? iconoCarpetaAbierta : iconoCarpetaCerrada;
        Console.WriteLine(carpetaAbierta ? "Abrir carpeta" : "Cerrar carpeta");
    }
    if (botonTerminal.IsClicked()) Console.WriteLine("Terminal");

    int anchoActual = Raylib.GetScreenWidth();
    int altoActual = Raylib.GetScreenHeight();
    barraMenu.Bounds = new Rectangle(0, AppTitleBar.Height, anchoActual, MenuBar.Height);

    explorador.Bounds = new Rectangle(margen, alturaHeader + margen, anchoExplorador, altoActual - alturaHeader - margen * 2);
    explorador.Update();

    // manija invisible: el hueco entre el explorador y el contenido de al lado se puede arrastrar
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

    // línea fina que separa el explorador del centro (no un panel, solo el trazo grabado)
    int xDivisorCentro = (int)(explorador.Bounds.X + explorador.Bounds.Width + anchoManijaResize / 2);
    Win95Style.DrawDivisorVertical(xDivisorCentro, (int)explorador.Bounds.Y, (int)explorador.Bounds.Height);

    barraApp.Draw();
    Raylib.EndDrawing();
}

Raylib.UnloadTexture(iconoNuevo);
Raylib.UnloadTexture(iconoGuardar);
Raylib.UnloadTexture(iconoPlay);
Raylib.UnloadTexture(iconoPausa);
Raylib.UnloadTexture(iconoLupa);
Raylib.UnloadTexture(iconoCarpetaCerrada);
Raylib.UnloadTexture(iconoCarpetaAbierta);
Raylib.UnloadTexture(iconoTerminal);
Raylib.CloseWindow();
