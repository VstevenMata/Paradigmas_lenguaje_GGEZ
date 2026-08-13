using Raylib_cs;
using ParadigmasLenguajePropio.UI;

const int anchoInicial = 1500;
const int altoInicial = 900;
const int anchoMinimo = 800;
const int altoMinimo = 450;

Raylib.SetConfigFlags(ConfigFlags.ResizableWindow);
Raylib.InitWindow(anchoInicial, altoInicial, "Paradigmas GGEZ");
Raylib.SetWindowMinSize(anchoMinimo, altoMinimo);
Raylib.SetTargetFPS(60);

var botonCorrer = new Button(600, 500, 200, 50, "Correr");

while (!Raylib.WindowShouldClose())
{
    int anchoActual = Raylib.GetScreenWidth();
    int altoActual = Raylib.GetScreenHeight();

    // el botón se recentra cada frame en base al tamaño actual, no al inicial
    botonCorrer.Bounds.X = (anchoActual - botonCorrer.Bounds.Width) / 2;
    botonCorrer.Bounds.Y = altoActual - 150;

    if (botonCorrer.IsClicked())
    {
        Console.WriteLine("Se hizo click en Correr");
    }

    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.RayWhite);
    Raylib.DrawText("Hola C fucking #", anchoActual / 2 - 150, altoActual / 2, 30, Color.DarkBlue);
    botonCorrer.Draw();
    Raylib.EndDrawing();
}

Raylib.CloseWindow();
