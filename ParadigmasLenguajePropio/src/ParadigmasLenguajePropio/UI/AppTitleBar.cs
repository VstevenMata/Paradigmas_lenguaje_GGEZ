using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class AppTitleBar
{
    public const int Height = 26;
    const int ButtonSize = 16;
    const int ButtonSizeGrande = 20;
    const int AnchoBusqueda = 320;
    const int AltoBusqueda = 18;
    const int LadoIconoBusqueda = 12;
    const double RetrasoTooltip = 0.5;

    public void Update()
    {
        if (WasClicked(CloseButton))
        {
            ShouldClose = true;
            return;
        }

        if (WasClicked(MinimizeButton))
        {
            Raylib.MinimizeWindow();
            return;
        }

        if (WasClicked(MaximizeButton))
        {
            if (Raylib.IsWindowMaximized()) Raylib.RestoreWindow();
            else Raylib.MaximizeWindow();
            return;
        }

        campoBusqueda.Bounds = AreaTextoBusqueda();
        campoBusqueda.Update();

        if (WasClicked(BotonMenuCampo)) Console.WriteLine("Menú del buscador");
        if (WasClicked(BotonDiseno)) Console.WriteLine("Alternar diseño");

        ActualizarHoverTooltip();
        UpdateArrastre();
    }

    // mismo mecanismo que los botones de la barra lateral: aparece tras un ratito quieto encima
    void ActualizarHoverTooltip()
    {
        var mouse = Raylib.GetMousePosition();
        string? textoActual = null;
        Rectangle? boundsActual = null;

        if (Raylib.CheckCollisionPointRec(mouse, BotonMenuCampo)) { textoActual = "Menú del buscador"; boundsActual = BotonMenuCampo; }
        else if (Raylib.CheckCollisionPointRec(mouse, BotonDiseno)) { textoActual = "Alternar diseño"; boundsActual = BotonDiseno; }

        if (textoActual != tooltipTexto)
        {
            tooltipTexto = textoActual;
            tooltipBounds = boundsActual;
            tiempoInicioHoverTooltip = Raylib.GetTime();
        }
    }

    void UpdateArrastre()
    {
        bool sobreControl = Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), CloseButton)
            || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), MaximizeButton)
            || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), MinimizeButton)
            || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), BotonMenuCampo)
            || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), BotonDiseno)
            || Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), CampoBusquedaBounds)
            || (ZonaExcluidaDelArrastre.HasValue && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), ZonaExcluidaDelArrastre.Value));

        if (!arrastrando && !sobreControl && Raylib.IsMouseButtonPressed(MouseButton.Left)
            && Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), Bounds))
        {
            arrastrando = true;
            posicionVentanaAlIniciarArrastre = Raylib.GetWindowPosition();
            mouseGlobalAlIniciarArrastre = posicionVentanaAlIniciarArrastre + Raylib.GetMousePosition();
        }

        if (!arrastrando) return;

        if (!Raylib.IsMouseButtonDown(MouseButton.Left))
        {
            arrastrando = false;
            return;
        }

        // sin decoración nativa no hay barra que el SO arrastre solo: se calcula a mano con la
        // posición global del cursor (ventana + mouse local), que no cambia si solo se movió la ventana
        Vector2 mouseGlobalActual = Raylib.GetWindowPosition() + Raylib.GetMousePosition();
        Vector2 delta = mouseGlobalActual - mouseGlobalAlIniciarArrastre;
        Vector2 nuevaPosicion = posicionVentanaAlIniciarArrastre + delta;
        Raylib.SetWindowPosition((int)nuevaPosicion.X, (int)nuevaPosicion.Y);
    }

    bool WasClicked(Rectangle r)
    {
        return Raylib.CheckCollisionPointRec(Raylib.GetMousePosition(), r) && Raylib.IsMouseButtonPressed(MouseButton.Left);
    }

    public void Draw()
    {
        // degradado azul-a-celeste como el Win95 real; en gris cuando la ventana pierde el foco
        bool enfocada = Raylib.IsWindowFocused();
        var colorInicio = enfocada ? Win95Style.TitleBarActivaInicio : Win95Style.TitleBarInactivaInicio;
        var colorFin = enfocada ? Win95Style.TitleBarActivaFin : Win95Style.TitleBarInactivaFin;
        Raylib.DrawRectangleGradientH((int)Bounds.X, (int)Bounds.Y, (int)Bounds.Width, (int)Bounds.Height, colorInicio, colorFin);

        DibujarCampoBusqueda();
        DibujarBotonIcono(BotonMenuCampo, IconoMenu, Win95Style.DrawChevronAbajoIcon);
        DibujarBotonIcono(BotonDiseno, IconoDiseno, Win95Style.DrawDisenoIcon);

        Win95Style.DrawMinimizeIcon(MinimizeButton);
        Win95Style.DrawMaximizeIcon(MaximizeButton);
        Win95Style.DrawCloseIcon(CloseButton);

        DibujarTooltip();
    }

    void DibujarTooltip()
    {
        if (tooltipTexto == null || !tooltipBounds.HasValue) return;
        if (Raylib.GetTime() - tiempoInicioHoverTooltip <= RetrasoTooltip) return;

        var b = tooltipBounds.Value;
        // si el botón está en la mitad derecha de la pantalla, el tooltip cuelga hacia la izquierda
        bool enMitadDerecha = b.X + b.Width / 2f > Bounds.Width / 2f;
        int anchoTooltip = Win95Style.MeasureText(tooltipTexto, 14) + 8;
        int xTooltip = enMitadDerecha ? (int)(b.X + b.Width) - anchoTooltip : (int)b.X;
        Win95Style.DrawTooltip(tooltipTexto, xTooltip, (int)(b.Y + b.Height + 4));
    }

    // botón con ícono png propio; si no se le asignó textura, cae al dibujo procedural de respaldo
    void DibujarBotonIcono(Rectangle r, Texture2D? icono, Action<Rectangle> dibujoDeRespaldo)
    {
        if (!icono.HasValue)
        {
            dibujoDeRespaldo(r);
            return;
        }

        Win95Style.DrawTitleButtonBase(r);
        var textura = icono.Value;
        float escala = Math.Min((r.Width - 4) / textura.Width, (r.Height - 4) / textura.Height);
        var posicion = new Vector2(r.X + (r.Width - textura.Width * escala) / 2, r.Y + (r.Height - textura.Height * escala) / 2);
        Raylib.DrawTextureEx(textura, posicion, 0f, escala, Color.White);
    }

    // todavía no busca nada, solo se puede escribir en el campo
    void DibujarCampoBusqueda()
    {
        var area = CampoBusquedaBounds;
        Raylib.DrawRectangleRec(area, Color.White);
        Win95Style.DrawBevel(area, sunken: true, thickness: 2);

        if (IconoBusqueda.HasValue)
        {
            var icono = IconoBusqueda.Value;
            float escala = (float)LadoIconoBusqueda / icono.Width;
            var posicionIcono = new Vector2(area.X + 4, area.Y + (area.Height - LadoIconoBusqueda) / 2f);
            Raylib.DrawTextureEx(icono, posicionIcono, 0f, escala, Color.White);
        }

        campoBusqueda.Bounds = AreaTextoBusqueda();
        campoBusqueda.Draw("Buscar en el proyecto", dibujarFondo: false);
    }

    // se usa igual en Update() y en Draw() para que el click y el cursor de texto coincidan
    Rectangle AreaTextoBusqueda()
    {
        var area = CampoBusquedaBounds;
        float xTexto = area.X + 4 + (IconoBusqueda.HasValue ? LadoIconoBusqueda + 4 : 0);
        return new Rectangle(xTexto, area.Y, area.X + area.Width - xTexto, area.Height);
    }

    public Texture2D? IconoBusqueda;
    public Texture2D? IconoMenu;
    public Texture2D? IconoDiseno;
    public bool ShouldClose { get; private set; }
    public bool TieneFocoEnCampo => campoBusqueda.TieneFoco;

    string? tooltipTexto;
    Rectangle? tooltipBounds;
    double tiempoInicioHoverTooltip;

    // el menú vive dentro de esta misma franja; quien lo posiciona informa su área para no arrastrar la ventana ahí
    public Rectangle? ZonaExcluidaDelArrastre;

    readonly CampoTexto campoBusqueda = new();

    bool arrastrando;
    Vector2 mouseGlobalAlIniciarArrastre;
    Vector2 posicionVentanaAlIniciarArrastre;

    Rectangle Bounds => new(0, 0, Raylib.GetScreenWidth(), Height);
    Rectangle CloseButton => new(Bounds.Width - ButtonSize - 4, 3, ButtonSize, ButtonSize);
    Rectangle MaximizeButton => new(CloseButton.X - ButtonSize - 5, 3, ButtonSize, ButtonSize);
    Rectangle MinimizeButton => new(MaximizeButton.X - ButtonSize - 5, 3, ButtonSize, ButtonSize);
    Rectangle CampoBusquedaBounds => new((Bounds.Width - AnchoBusqueda) / 2, (Height - AltoBusqueda) / 2, AnchoBusqueda, AltoBusqueda);

    // un par de botones junto al buscador, igual que el menú y el selector de diseño de VSCode
    Rectangle BotonMenuCampo => new(CampoBusquedaBounds.X + CampoBusquedaBounds.Width + 8, (Height - ButtonSizeGrande) / 2, ButtonSizeGrande, ButtonSizeGrande);
    Rectangle BotonDiseno => new(BotonMenuCampo.X + ButtonSizeGrande + 5, (Height - ButtonSizeGrande) / 2, ButtonSizeGrande, ButtonSizeGrande);
}
