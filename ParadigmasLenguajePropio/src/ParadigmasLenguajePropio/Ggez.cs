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

const int ladoBoton = 36;
const int margen = 4;
const int espacio = 4;
int y = AppTitleBar.Height + margen;

var botonNuevo = new Button(margen, y, ladoBoton, ladoBoton, "") { Icon = iconoNuevo };
var botonGuardar = new Button(margen + (ladoBoton + espacio) * 1, y, ladoBoton, ladoBoton, "") { Icon = iconoGuardar };
var botonPlay = new Button(margen + (ladoBoton + espacio) * 2, y, ladoBoton, ladoBoton, "") { Icon = iconoPlay };
var botonPausa = new Button(margen + (ladoBoton + espacio) * 3, y, ladoBoton, ladoBoton, "") { Icon = iconoPausa };
var botonLupa = new Button(margen + (ladoBoton + espacio) * 4, y, ladoBoton, ladoBoton, "") { Icon = iconoLupa };
var botonCarpeta = new Button(margen + (ladoBoton + espacio) * 5, y, ladoBoton, ladoBoton, "") { Icon = iconoCarpetaCerrada };

var botonesToolbar = new[] { botonNuevo, botonGuardar, botonPlay, botonPausa, botonLupa, botonCarpeta };
bool carpetaAbierta = false;

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

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Win95Style.Background);
    foreach (var boton in botonesToolbar)
    {
        boton.Draw();
    }
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
Raylib.CloseWindow();
