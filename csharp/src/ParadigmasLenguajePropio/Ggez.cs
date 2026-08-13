using Raylib_cs;

const int screenWidth = 800;
const int screenHeight = 450;

Raylib.InitWindow(screenWidth, screenHeight, "Paradigmas GGEZ");
Raylib.SetTargetFPS(60);

while (!Raylib.WindowShouldClose())
{
    Raylib.BeginDrawing();
    Raylib.ClearBackground(Color.RayWhite);
    Raylib.DrawText("Hola C fucking #", 260, 200, 30, Color.DarkBlue);
    Raylib.EndDrawing();
}

Raylib.CloseWindow();
