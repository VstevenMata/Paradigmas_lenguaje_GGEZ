using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class BarraBusqueda
{
    const int Alto = 28;
    const int AnchoCampo = 150;
    const int AnchoCampoReemplazo = 150;
    const int LadoBoton = 22;
    const int Padding = 4;

    public void Update()
    {
        if (!Visible) return;

        if (Raylib.IsKeyPressed(KeyboardKey.Escape)) { Visible = false; return; }

        campoBuscar.Update();
        if (MostrarReemplazo) campoReemplazar.Update();

        if (botonCerrar.IsClicked()) { Visible = false; return; }
        if (botonExpandir.IsClicked()) MostrarReemplazo = !MostrarReemplazo;
        if (botonMayusMinus.IsClicked()) DistingueMayusculas = !DistingueMayusculas;

        // reaccionar cuando cambia lo escrito o el toggle de mayúsculas, así busca "en vivo"
        bool cambioPatron = campoBuscar.Texto != ultimoPatronBuscado || DistingueMayusculas != ultimoDistingueMayus;
        if (cambioPatron)
        {
            ultimoPatronBuscado = campoBuscar.Texto;
            ultimoDistingueMayus = DistingueMayusculas;
            if (Editor != null) (contadorActual, contadorTotal) = Editor.Buscar(campoBuscar.Texto, DistingueMayusculas, siguiente: true);
        }

        bool shift = Raylib.IsKeyDown(KeyboardKey.LeftShift) || Raylib.IsKeyDown(KeyboardKey.RightShift);
        if (campoBuscar.TieneFoco && Raylib.IsKeyPressed(KeyboardKey.Enter) && Editor != null)
            (contadorActual, contadorTotal) = Editor.Buscar(campoBuscar.Texto, DistingueMayusculas, siguiente: !shift);

        if (botonAnterior.IsClicked() && Editor != null)
            (contadorActual, contadorTotal) = Editor.Buscar(campoBuscar.Texto, DistingueMayusculas, siguiente: false);
        if (botonSiguiente.IsClicked() && Editor != null)
            (contadorActual, contadorTotal) = Editor.Buscar(campoBuscar.Texto, DistingueMayusculas, siguiente: true);

        if (MostrarReemplazo && Editor != null)
        {
            if (botonReemplazar.IsClicked())
                (contadorActual, contadorTotal) = Editor.Reemplazar(campoBuscar.Texto, campoReemplazar.Texto, DistingueMayusculas);
            if (botonReemplazarTodo.IsClicked())
            {
                Editor.ReemplazarTodo(campoBuscar.Texto, campoReemplazar.Texto, DistingueMayusculas);
                (contadorActual, contadorTotal) = Editor.Buscar(campoBuscar.Texto, DistingueMayusculas, siguiente: true);
            }
        }
    }

    // se pega a la esquina del área de texto (no del panel entero), antes de la scrollbar
    public void Posicionar(Rectangle areaEditor)
    {
        float ancho = AnchoBarra();
        float x = areaEditor.X + areaEditor.Width - PanelCentral.AnchoReservadoDerecha - ancho;
        float y = areaEditor.Y + PanelCentral.AltoCabecera;
        Bounds = new Rectangle(x, y, ancho, MostrarReemplazo ? Alto * 2 : Alto);
        CalcularLayout();
    }

    // se calcula acá (no en Draw) para que Update() ya tenga los Bounds correctos antes de procesar clicks
    void CalcularLayout()
    {
        float x = Bounds.X + Padding;
        float y = Bounds.Y + (Alto - LadoBoton) / 2f;

        botonExpandir.Bounds = new Rectangle(x, y, LadoBoton, LadoBoton);
        x += LadoBoton + Padding;

        campoBuscar.Bounds = new Rectangle(x, y, AnchoCampo, LadoBoton);
        x += AnchoCampo + Padding;

        int anchoContador = Win95Style.MeasureText("0/0", 14) + 4;
        xContador = x;
        yContador = y + 4;
        x += anchoContador + Padding;

        botonMayusMinus.Bounds = new Rectangle(x, y, LadoBoton, LadoBoton);
        x += LadoBoton + Padding;

        botonAnterior.Bounds = new Rectangle(x, y, LadoBoton, LadoBoton);
        x += LadoBoton + 2;

        botonSiguiente.Bounds = new Rectangle(x, y, LadoBoton, LadoBoton);
        x += LadoBoton + Padding;

        botonCerrar.Bounds = new Rectangle(x, y, LadoBoton, LadoBoton);

        float x2 = Bounds.X + Padding + LadoBoton + Padding;
        float y2 = Bounds.Y + Alto + (Alto - LadoBoton) / 2f;

        campoReemplazar.Bounds = new Rectangle(x2, y2, AnchoCampoReemplazo, LadoBoton);
        x2 += AnchoCampoReemplazo + Padding;

        botonReemplazar.Bounds = new Rectangle(x2, y2, 80, LadoBoton);
        x2 += 80 + Padding;

        botonReemplazarTodo.Bounds = new Rectangle(x2, y2, 100, LadoBoton);
    }

    public void Draw()
    {
        if (!Visible) return;

        Raylib.DrawRectangleRec(Bounds, Win95Style.Background);
        Win95Style.DrawBevel(Bounds, sunken: false, thickness: 2);

        DibujarFilaBuscar();
        if (MostrarReemplazo) DibujarFilaReemplazar();
    }

    void DibujarFilaBuscar()
    {
        botonExpandir.Text = MostrarReemplazo ? "-" : "+";
        botonExpandir.Draw();

        campoBuscar.Draw("Buscar");

        Win95Style.DrawText($"{contadorActual}/{contadorTotal}", (int)xContador, (int)yContador, 14, Win95Style.TextoDeshabilitado);

        bool hundidoMayusMinus = botonMayusMinus.IsPressed() || DistingueMayusculas;
        Raylib.DrawRectangleRec(botonMayusMinus.Bounds, botonMayusMinus.IsHovered() ? Color.LightGray : Win95Style.Background);
        Win95Style.DrawBevel(botonMayusMinus.Bounds, sunken: hundidoMayusMinus);
        int anchoAa = Win95Style.MeasureText("Aa", 12);
        Win95Style.DrawText("Aa", (int)(botonMayusMinus.Bounds.X + (LadoBoton - anchoAa) / 2), (int)(botonMayusMinus.Bounds.Y + 5), 12, Color.Black);

        DibujarBotonFlecha(botonAnterior, arriba: true);
        DibujarBotonFlecha(botonSiguiente, arriba: false);

        Raylib.DrawRectangleRec(botonCerrar.Bounds, botonCerrar.IsHovered() ? Color.LightGray : Win95Style.Background);
        Win95Style.DrawBevel(botonCerrar.Bounds, sunken: botonCerrar.IsPressed());
        Win95Style.DrawCloseIcon(new Rectangle(botonCerrar.Bounds.X + 3, botonCerrar.Bounds.Y + 3, LadoBoton - 6, LadoBoton - 6));
    }

    void DibujarFilaReemplazar()
    {
        campoReemplazar.Draw("Reemplazar");

        botonReemplazar.Text = "Reemplazar";
        botonReemplazar.FontSize = 12;
        botonReemplazar.Draw();

        botonReemplazarTodo.Text = "Reempl. todo";
        botonReemplazarTodo.FontSize = 12;
        botonReemplazarTodo.Draw();
    }

    void DibujarBotonFlecha(Button boton, bool arriba)
    {
        Raylib.DrawRectangleRec(boton.Bounds, boton.IsHovered() ? Color.LightGray : Win95Style.Background);
        Win95Style.DrawBevel(boton.Bounds, sunken: boton.IsPressed());

        float cx = boton.Bounds.X + boton.Bounds.Width / 2;
        float cy = boton.Bounds.Y + boton.Bounds.Height / 2;
        if (arriba)
            Raylib.DrawTriangle(new(cx, cy - 4), new(cx - 4, cy + 3), new(cx + 4, cy + 3), Color.Black);
        else
            Raylib.DrawTriangle(new(cx, cy + 4), new(cx + 4, cy - 3), new(cx - 4, cy - 3), Color.Black);
    }

    // mismos incrementos que CalcularLayout, así el ancho calculado siempre coincide con lo dibujado
    float AnchoBarra() => MostrarReemplazo ? MathF.Max(AnchoFilaBuscar(), AnchoFilaReemplazar()) : AnchoFilaBuscar();

    float AnchoFilaBuscar()
    {
        int anchoContador = Win95Style.MeasureText("0/0", 14) + 4;
        return Padding + LadoBoton + Padding + AnchoCampo + Padding + anchoContador + Padding
            + LadoBoton + Padding + LadoBoton + 2 + LadoBoton + Padding + LadoBoton + Padding;
    }

    float AnchoFilaReemplazar()
    {
        return Padding + LadoBoton + Padding + AnchoCampoReemplazo + Padding + 80 + Padding + 100 + Padding;
    }

    public bool Visible;
    public bool MostrarReemplazo;
    public bool DistingueMayusculas;
    public PanelCentral? Editor;
    public Rectangle Bounds { get; private set; }
    public bool TieneFocoEnCampo => campoBuscar.TieneFoco || campoReemplazar.TieneFoco;

    float xContador, yContador;
    string ultimoPatronBuscado = "";
    bool ultimoDistingueMayus;
    int contadorActual;
    int contadorTotal;

    readonly CampoTexto campoBuscar = new();
    readonly CampoTexto campoReemplazar = new();
    readonly Button botonExpandir = new(0, 0, LadoBoton, LadoBoton, "+") { FontSize = 14 };
    readonly Button botonMayusMinus = new(0, 0, LadoBoton, LadoBoton, "Aa");
    readonly Button botonAnterior = new(0, 0, LadoBoton, LadoBoton, "");
    readonly Button botonSiguiente = new(0, 0, LadoBoton, LadoBoton, "");
    readonly Button botonCerrar = new(0, 0, LadoBoton, LadoBoton, "");
    readonly Button botonReemplazar = new(0, 0, 80, LadoBoton, "Reemplazar");
    readonly Button botonReemplazarTodo = new(0, 0, 100, LadoBoton, "Reempl. todo");
}
