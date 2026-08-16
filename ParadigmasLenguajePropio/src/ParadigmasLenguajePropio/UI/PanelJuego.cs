using System.Numerics;
using Raylib_cs;

namespace ParadigmasLenguajePropio.UI;

public class PanelJuego
{
    // VARIABLES CONSTANTES
    const int AltoViewportSugerido = 90;
    const int AltoBarraNivel = 20;
    const int SegmentosNivel = 24;
    const int AnchoBoton = 72;
    const int AltoBoton = 24;
    const int FontSizeBoton = 16;
    const int Margen = 8;
    const int AnchoPestana = 28;
    const int LadoPersonaje = 48;
    const float VelocidadAnimacion = 1400f;

    public void Update()
    {
        float objetivo = Oculto ? AnchoPestana : boundsHome.Width;
        float paso = VelocidadAnimacion * Raylib.GetFrameTime();
        anchoActual = MathF.Abs(objetivo - anchoActual) <= paso ? objetivo : anchoActual + MathF.Sign(objetivo - anchoActual) * paso;

    
        //nunca se va de pantalla y siempre se puede volver a abrir
        panel.Bounds = new Rectangle(boundsHome.X + boundsHome.Width - anchoActual, boundsHome.Y, anchoActual, boundsHome.Height);
        panel.Minimized = Oculto;

        panel.Update();
        if (panel.BotonExtraClickeado()) Oculto = !Oculto;

        if (panel.Minimized) return;

        var area = panel.ContentArea();
        var filaBotones = FilaBotones(area, AlturaViewport(area));
        botonOk.Bounds = new Rectangle(filaBotones.X + filaBotones.Width - AnchoBoton * 2 - 6, filaBotones.Y, AnchoBoton, AltoBoton);
        botonCancel.Bounds = new Rectangle(filaBotones.X + filaBotones.Width - AnchoBoton, filaBotones.Y, AnchoBoton, AltoBoton);

        if (botonOk.IsClicked()) Console.WriteLine("Runner: OK");
        if (botonCancel.IsClicked()) Console.WriteLine("Runner: Cancel");
    }

    public void Draw()
    {
        panel.Draw(DibujarContenido);
    }

    void DibujarContenido(Rectangle area)
    {
        // vista del juegoodavía sin diseñar, solo el marco hundido reservando el lugar.
        // ocupa todo lo que sobra del alto del panel, así las dos ventanas llenan la columna
        float alturaViewport = AlturaViewport(area);
        var viewport = new Rectangle(area.X, area.Y, area.Width, alturaViewport);
        Raylib.DrawRectangleRec(viewport, Color.White);
        Win95Style.DrawBevel(viewport, sunken: true, thickness: 2);
        DibujarPersonaje(viewport);

        DibujarBarraNivel(new Rectangle(area.X, viewport.Y + viewport.Height + Margen, area.Width, AltoBarraNivel));

        var filaBotones = FilaBotones(area, alturaViewport);
        if (EnPausa)
        {
            Win95Style.DrawText("En pausa", (int)filaBotones.X, (int)filaBotones.Y + 3, 17, Color.Black);
        }

        botonOk.Draw();
        botonCancel.Draw();
    }

    float AlturaViewport(Rectangle area)
    {
        return area.Height - Margen - AltoBarraNivel - Margen - AltoBoton;
    }

    void DibujarPersonaje(Rectangle viewport)
    {
        int lado = Math.Min(LadoPersonaje, (int)MathF.Min(viewport.Width, viewport.Height));
        float escala = lado / (float)iconoPersonaje64px.Width;
        var posicion = new Vector2(viewport.X + (viewport.Width - lado) / 2f, viewport.Y + (viewport.Height - lado) / 2f);
        Raylib.DrawTextureEx(iconoPersonaje64px, posicion, 0f, escala, Color.White);
    }

    void DibujarBarraNivel(Rectangle fila)
    {
        Win95Style.DrawText("Nivel:", (int)fila.X, (int)fila.Y + 2, 16, Color.Black);

        int anchoEtiqueta = Win95Style.MeasureText("Nivel:", 16) + 8;
        var pista = new Rectangle(fila.X + anchoEtiqueta, fila.Y, fila.Width - anchoEtiqueta, fila.Height);
        Raylib.DrawRectangleRec(pista, Color.White);
        Win95Style.DrawBevel(pista, sunken: true, thickness: 1);

        float anchoSegmento = (pista.Width - 4) / SegmentosNivel;
        int llenos = (int)(SegmentosNivel * Progreso);
        for (int i = 0; i < llenos; i++)
        {
            var segmento = new Rectangle(pista.X + 2 + i * anchoSegmento, pista.Y + 2, anchoSegmento - 1, pista.Height - 4);
            Raylib.DrawRectangleRec(segmento, new Color(0, 0, 160, 255));
        }
    }

    Rectangle FilaBotones(Rectangle area, float alturaViewport)
    {
        return new Rectangle(area.X, area.Y + alturaViewport + Margen + AltoBarraNivel + Margen, area.Width, AltoBoton);
    }

    public Rectangle Bounds
    {
        get => boundsHome;
        set => boundsHome = value;
    }

    public int AlturaSugerida => Panel.TitleBarHeight + 8 + AltoViewportSugerido + Margen + AltoBarraNivel + Margen + AltoBoton;

    // hasta dónde llega ahora mismo el borde izquierdo
    // central sepa cuánto espacio recuperar cuando esta ventana está cerrada
    public float BordeIzquierdoActual => boundsHome.X + boundsHome.Width - anchoActual;

    public bool EnPausa = true;
    public float Progreso = 0.55f;
    public bool Oculto;

    Rectangle boundsHome;
    float anchoActual;

    readonly Panel panel;
    readonly Texture2D iconoPersonaje64px;
    readonly Button botonOk = new(0, 0, AnchoBoton, AltoBoton, "OK") { FontSize = FontSizeBoton };
    readonly Button botonCancel = new(0, 0, AnchoBoton, AltoBoton, "Cancel") { FontSize = FontSizeBoton };

    public PanelJuego(Texture2D iconoPersonaje64px, Texture2D iconoPuertaAbierta, Texture2D iconoPuertaCerrada)
    {
        this.iconoPersonaje64px = iconoPersonaje64px;

        panel = new Panel(0, 0, 0, 0, "Innvacion")
        {
            MostrarMinimizar = false,
            MostrarMaximizar = false,
            MostrarCerrar = false,
            MostrarBotonExtra = true,
            IconoBotonExtra = r =>
            {
                var icono = Oculto ? iconoPuertaCerrada : iconoPuertaAbierta;
                float escala = r.Width / icono.Width;
                Raylib.DrawTextureEx(icono, new Vector2(r.X, r.Y), 0f, escala, Color.White);
            }
        };
    }
}
