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

while (!Raylib.WindowShouldClose() && !barraApp.ShouldClose)
{
    barraApp.Update();

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Win95Style.Background);
    barraApp.Draw();
    Raylib.EndDrawing();
}

Raylib.CloseWindow();
