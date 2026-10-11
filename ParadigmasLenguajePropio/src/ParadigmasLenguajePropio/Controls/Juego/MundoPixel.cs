using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Threading;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.Controls;

// Pantalla de arriba de la consola: la zona (cielo, camino con una bandera por mision, puerta y jefe) o el mapa de zonas
// Todo sale de SpritesMundo pintado con los colores TemaJuego* del tema activo, sin imagenes
// La secuencia (cuando caminar, pelear, abrir la puerta) la decide PanelJuegoViewModel; aca solo se anima
public class MundoPixel : Control
{
    // 160x96 pixeles de juego: proporcion 5:3 como la pantalla de arriba de una consola de dos pantallas
    const int Columnas = 20, Filas = 12, Tile = 8;
    const int Ancho = Columnas * Tile, Alto = Filas * Tile;
    const int FilasCielo = 4, FilaCamino = 8;
    const double SegundosPorTramo = 0.45;
    const double SegundosPorNodo = 0.5;
    const double SegundosPorCuadro = 0.12;
    const double DuracionSacudida = 0.35;
    const double DuracionCartel = 2.2;
    public const double DuracionPelea = 3.6;
    static readonly TimeSpan TickQuieto = TimeSpan.FromMilliseconds(100);
    static readonly TimeSpan TickMoviendo = TimeSpan.FromMilliseconds(16);
    static readonly double[] Golpes = [0.5, 1.5, 2.5];

    // Nodos del mapa en zigzag: fila de ida y fila de vuelta
    static readonly (int Col, int Fila)[] NodosMapa =
        [(1, 3), (5, 3), (9, 3), (13, 3), (17, 3), (17, 8), (13, 8), (9, 8), (5, 8), (1, 8)];

    public static readonly StyledProperty<int> ZonaProperty = AvaloniaProperty.Register<MundoPixel, int>(nameof(Zona));
    public static readonly StyledProperty<double> AvanceProperty = AvaloniaProperty.Register<MundoPixel, double>(nameof(Avance));
    public static readonly StyledProperty<int> TotalProperty = AvaloniaProperty.Register<MundoPixel, int>(nameof(Total));
    public static readonly StyledProperty<int> BanderasLogradasProperty = AvaloniaProperty.Register<MundoPixel, int>(nameof(BanderasLogradas));
    public static readonly StyledProperty<int> PosicionMapaProperty = AvaloniaProperty.Register<MundoPixel, int>(nameof(PosicionMapa));
    public static readonly StyledProperty<bool> VerMapaProperty = AvaloniaProperty.Register<MundoPixel, bool>(nameof(VerMapa));
    public static readonly StyledProperty<bool> MostrarHeroeProperty = AvaloniaProperty.Register<MundoPixel, bool>(nameof(MostrarHeroe), true);
    public static readonly StyledProperty<bool> PuertaAbiertaProperty = AvaloniaProperty.Register<MundoPixel, bool>(nameof(PuertaAbierta));
    public static readonly StyledProperty<bool> JefeVisibleProperty = AvaloniaProperty.Register<MundoPixel, bool>(nameof(JefeVisible));
    public static readonly StyledProperty<bool> PeleaJefeProperty = AvaloniaProperty.Register<MundoPixel, bool>(nameof(PeleaJefe));
    public static readonly StyledProperty<int> SacudidasProperty = AvaloniaProperty.Register<MundoPixel, int>(nameof(Sacudidas));
    public static readonly StyledProperty<string> CartelProperty = AvaloniaProperty.Register<MundoPixel, string>(nameof(Cartel), "");
    public static readonly StyledProperty<string> MensajeFinalProperty = AvaloniaProperty.Register<MundoPixel, string>(nameof(MensajeFinal), "");

    public int Zona { get => GetValue(ZonaProperty); set => SetValue(ZonaProperty, value); }
    // Tramos recorridos: 1.5 = a mitad de camino entre la bandera 1 y la 2; Total + 1 = en la puerta
    public double Avance { get => GetValue(AvanceProperty); set => SetValue(AvanceProperty, value); }
    public int Total { get => GetValue(TotalProperty); set => SetValue(TotalProperty, value); }
    // Bit k prendido = la mision k ya esta completa (las banderas se pintan por mision, no por avance)
    public int BanderasLogradas { get => GetValue(BanderasLogradasProperty); set => SetValue(BanderasLogradasProperty, value); }
    // Nodo del mapa donde esta el heroe; al subir camina por el camino, al bajar (reinicio) salta
    public int PosicionMapa { get => GetValue(PosicionMapaProperty); set => SetValue(PosicionMapaProperty, value); }
    public bool VerMapa { get => GetValue(VerMapaProperty); set => SetValue(VerMapaProperty, value); }
    public bool MostrarHeroe { get => GetValue(MostrarHeroeProperty); set => SetValue(MostrarHeroeProperty, value); }
    public bool PuertaAbierta { get => GetValue(PuertaAbiertaProperty); set => SetValue(PuertaAbiertaProperty, value); }
    public bool JefeVisible { get => GetValue(JefeVisibleProperty); set => SetValue(JefeVisibleProperty, value); }
    // Mientras es true corre la pelea (DuracionPelea segundos): tres golpes y el jefe explota
    public bool PeleaJefe { get => GetValue(PeleaJefeProperty); set => SetValue(PeleaJefeProperty, value); }
    // Cada cambio hace que el heroe se sacuda (el programa fallo)
    public int Sacudidas { get => GetValue(SacudidasProperty); set => SetValue(SacudidasProperty, value); }
    // Cartel que baja, se queda DuracionCartel y sube; un texto nuevo lo vuelve a mostrar
    public string Cartel { get => GetValue(CartelProperty); set => SetValue(CartelProperty, value); }
    // Mensaje fijo en el centro (el final del juego); vacio = no hay
    public string MensajeFinal { get => GetValue(MensajeFinalProperty); set => SetValue(MensajeFinalProperty, value); }

    record struct Chispa(double X, double Y, double Vx, double Vy, double Vida, char Color);

    readonly DispatcherTimer _timer = new();
    readonly List<Chispa> _chispas = new();
    readonly Random _azar = new();
    DateTime _ultimoTick;
    double _posicion;          // donde se dibuja el heroe en la zona, en tramos; se acerca a Avance
    double _llegada;           // ultimo avance al que llego, para tirar chispas solo al avanzar
    double _posMapa;           // donde se dibuja el heroe en el mapa, en nodos; se acerca a PosicionMapa
    double _reloj;             // segundos desde que se mostro: nubes, respirar y cuadros de caminar
    double _sacudida;          // segundos que le quedan a la sacudida
    double _tCartel = DuracionCartel;
    double _tPelea = -1;       // -1 = sin pelea
    bool _jefeExploto;

    static MundoPixel()
    {
        AffectsRender<MundoPixel>(TotalProperty, BanderasLogradasProperty, VerMapaProperty, MostrarHeroeProperty,
            PuertaAbiertaProperty, JefeVisibleProperty, MensajeFinalProperty);
        ZonaProperty.Changed.AddClassHandler<MundoPixel>((c, _) => c.SaltarZona());
        AvanceProperty.Changed.AddClassHandler<MundoPixel>((c, _) =>
        {
            if (c.VisualRoot == null) c.SaltarZona();
            else c.Acelerar();
        });
        PosicionMapaProperty.Changed.AddClassHandler<MundoPixel>((c, _) =>
        {
            if (c.VisualRoot == null || c.PosicionMapa < c._posMapa) c._posMapa = c.PosicionMapa;
            c.Acelerar();
        });
        SacudidasProperty.Changed.AddClassHandler<MundoPixel>((c, _) => { c._sacudida = DuracionSacudida; c.Acelerar(); });
        CartelProperty.Changed.AddClassHandler<MundoPixel>((c, _) =>
        {
            if (string.IsNullOrEmpty(c.Cartel)) return;
            c._tCartel = 0;
            c.Acelerar();
        });
        PeleaJefeProperty.Changed.AddClassHandler<MundoPixel>((c, _) =>
        {
            c._tPelea = c.PeleaJefe ? 0 : -1;
            c._jefeExploto = false;
            c.Acelerar();
        });
    }

    public MundoPixel() => _timer.Tick += (_, _) => Avanzar();

    // Mismo alto para el mismo ancho: la pantalla no se estira
    protected override Size MeasureOverride(Size disponible)
    {
        double ancho = double.IsInfinity(disponible.Width) ? Ancho * 2 : disponible.Width;
        return new Size(ancho, Math.Round(ancho * Filas / Columnas));
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        SaltarZona();
        _posMapa = PosicionMapa;
        TemaService.Cambio += InvalidateVisual;
        _ultimoTick = DateTime.UtcNow;
        _timer.Interval = TickQuieto;
        _timer.Start();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        TemaService.Cambio -= InvalidateVisual;
        _timer.Stop();
    }

    // Cambio de zona o arranque: el heroe aparece en su lugar sin caminar ni chispas
    void SaltarZona()
    {
        _posicion = _llegada = Avance;
        _chispas.Clear();
        InvalidateVisual();
    }

    void Acelerar() => _timer.Interval = TickMoviendo;

    bool Caminando => _posicion != Avance;
    bool CaminandoMapa => _posMapa != PosicionMapa;
    bool Peleando => _tPelea >= 0 && _tPelea < DuracionPelea;

    static double Acercar(double desde, double hasta, double paso) =>
        Math.Abs(hasta - desde) <= paso ? hasta : desde + Math.Sign(hasta - desde) * paso;

    // Quieto el timer va a 10 cuadros por segundo (nubes y respirar); rapido solo mientras algo se mueve
    void Avanzar()
    {
        var ahora = DateTime.UtcNow;
        double dt = Math.Min(0.1, (ahora - _ultimoTick).TotalSeconds);
        _ultimoTick = ahora;
        _reloj += dt;
        _tCartel += dt;
        _sacudida = Math.Max(0, _sacudida - dt);

        _posicion = Acercar(_posicion, Avance, dt / SegundosPorTramo);
        _posMapa = Acercar(_posMapa, PosicionMapa, dt / SegundosPorNodo);
        if (!Caminando && Avance > _llegada)
        {
            // Bandera entera = muchas chispas; medio tramo = pocas; la puerta = explosion
            bool puerta = Avance > Math.Max(1, Total);
            bool bandera = Math.Abs(Avance - Math.Round(Avance)) < 1e-9;
            TirarChispas(XTramo(Avance) + 4, FilaCamino * Tile - 8, puerta ? 26 : bandera ? 14 : 6);
            _llegada = Avance;
        }

        if (_tPelea >= 0)
        {
            _tPelea += dt;
            if (_tPelea >= 3.0 && !_jefeExploto)
            {
                var (jx, jy) = PosJefe();
                TirarChispas(jx + 8, jy + 8, 40);
                _jefeExploto = true;
            }
        }

        for (int i = _chispas.Count - 1; i >= 0; i--)
        {
            var c = _chispas[i];
            c = c with { X = c.X + c.Vx * dt, Y = c.Y + c.Vy * dt, Vy = c.Vy + 140 * dt, Vida = c.Vida - dt };
            if (c.Vida <= 0) _chispas.RemoveAt(i);
            else _chispas[i] = c;
        }

        bool algoSeMueve = Caminando || CaminandoMapa || Peleando || _sacudida > 0 || _chispas.Count > 0 || _tCartel < DuracionCartel;
        if (!algoSeMueve) _timer.Interval = TickQuieto;
        InvalidateVisual();
    }

    void TirarChispas(double x, double y, int cantidad)
    {
        for (int i = 0; i < cantidad; i++)
        {
            double angulo = _azar.NextDouble() * Math.PI + Math.PI;
            double velocidad = 30 + _azar.NextDouble() * 45;
            _chispas.Add(new(x, y, Math.Cos(angulo) * velocidad, Math.Sin(angulo) * velocidad, 0.5 + _azar.NextDouble() * 0.4,
                i % 2 == 0 ? 'L' : 'W'));
        }
    }

    // x en pixeles de juego del tramo (0 = inicio, k = bandera k, Total + 1 = puerta)
    double XTramo(double tramo)
    {
        double inicio = Tile, fin = (Columnas - 3) * Tile;
        return inicio + tramo * (fin - inicio) / (Math.Max(1, Total) + 1);
    }

    double XPuerta => Math.Round(XTramo(Math.Max(1, Total) + 1)) - 2;

    // El jefe flota arriba del camino, antes de la puerta
    (double X, double Y) PosJefe() => (XPuerta - 20, FilasCielo * Tile - 4 + Math.Round(Math.Sin(_reloj * 2) * 2));

    public override void Render(DrawingContext ctx)
    {
        // Escala exacta para llenar la pantalla; Lienzo redondea cada borde y el pixel art queda nitido igual
        double u = Math.Min(Bounds.Width / Ancho, Bounds.Height / Alto);
        if (u <= 0) return;
        double ox = Math.Floor((Bounds.Width - Ancho * u) / 2);
        double oy = Math.Floor((Bounds.Height - Alto * u) / 2);
        var l = new Lienzo(ctx, LeerPaleta(), u, ox, oy);

        // Nada se dibuja fuera de la pantalla: nubes, chispas y carteles quedan adentro
        using (ctx.PushClip(new Rect(ox, oy, Ancho * u, Alto * u)))
        {
            if (VerMapa) DibujarMapa(l);
            else DibujarZona(l);
            DibujarCartel(l);
            if (!string.IsNullOrEmpty(MensajeFinal)) DibujarMensajeFinal(l);
        }
    }

    void DibujarZona(Lienzo l)
    {
        string tema = TemaService.Actual.ToString();
        int total = Math.Max(1, Total);

        var escenario = (Escenario)Math.Clamp(Zona, 0, (int)Escenario.Guarida);
        DibujarCielo(l, escenario);
        l.Suelo(0, FilasCielo * Tile, Ancho, Alto - FilasCielo * Tile);

        // Decoracion repetible por zona: la misma zona siempre se ve igual; el bosque va mas tupido
        var azar = new Random(Zona * 7919 + 17);
        var decoracion = SpritesMundo.Decoracion.GetValueOrDefault(tema) ?? SpritesMundo.Decoracion["Win95"];
        int[] filas = UsaFondo(escenario) ? [10, 11] : escenario == Escenario.Rio ? [4, 5] : [4, 5, 10, 11];
        int densidad = escenario == Escenario.Bosque ? 2 : 3;
        foreach (int fila in filas)
            for (int col = 0; col < Columnas; col++)
                if (azar.Next(densidad) == 0) l.Sprite(decoracion, col * Tile, fila * Tile);

        DibujarEscenario(l, escenario);

        l.Camino(Tile, FilaCamino * Tile, (Columnas - 2) * Tile, Tile);

        for (int k = 1; k <= total; k++)
        {
            bool lograda = (BanderasLogradas & (1 << (k - 1))) != 0;
            l.Sprite(lograda ? SpritesMundo.BanderaLograda : SpritesMundo.Bandera, Math.Round(XTramo(k)), FilaCamino * Tile - 12);
        }

        l.Sprite(PuertaAbierta ? SpritesMundo.PuertaAbierta : SpritesMundo.PuertaCerrada, XPuerta, (FilaCamino + 1) * Tile - 16);
        DibujarJefe(l);

        if (MostrarHeroe)
        {
            // Caminando alterna cuadros y rebota; quieto respira un pixel cada medio segundo
            int cuadro = Caminando ? (int)(_reloj / SegundosPorCuadro) % 2 : 0;
            double rebote = Caminando ? cuadro : (int)(_reloj / 0.5) % 2;
            double x = XTramo(_posicion) - 4 + (_sacudida > 0 ? Math.Sin(_reloj * 60) * 1.5 : 0);
            double y = FilaCamino * Tile + Tile / 2 - 16 - rebote;
            (x, y) = SaltoDePelea(x, y);
            l.Sprite(Heroe(tema)[cuadro], x, y);
        }

        foreach (var c in _chispas) l.Relleno(c.Color, c.X, c.Y, 1, 1);
    }

    // Una por zona de Models/Mundo, en el mismo orden; solo cambia el escenario, los colores son del tema
    enum Escenario { Aldea, Pradera, Bosque, Rio, Cueva, Torre, Mercado, Castillo, Gremio, Guarida }

    // Estos ocupan las filas 4-5 con su escenario, ahi no va decoracion
    static bool UsaFondo(Escenario e) => e is Escenario.Aldea or Escenario.Torre or Escenario.Mercado
        or Escenario.Castillo or Escenario.Gremio;

    void DibujarCielo(Lienzo l, Escenario e)
    {
        const int alto = FilasCielo * Tile;
        if (e == Escenario.Cueva)
        {
            // Techo de roca con estalactitas en vez de cielo
            l.Relleno('K', 0, 0, Ancho, alto);
            for (int x = 2; x < Ancho; x += 13)
                for (int f = 0; f < 4; f++) l.Relleno('T', x + f, f * 4, 8 - f * 2, 4);
            return;
        }

        l.Relleno('C', 0, 0, Ancho, alto);
        if (e == Escenario.Guarida)
        {
            // Cielo de tormenta con luna roja
            l.Velo(0, 0, Ancho, alto, 0.55);
            l.Circulo('D', 136, 12, 7);
            return;
        }
        if (TemaService.Actual == Tema.MegaMan) l.Estrellas(Ancho, alto, _reloj);
        else
            for (int n = 0; n < 3; n++)
            {
                // Cada nube a su velocidad; da la vuelta por fuera de la pantalla (el recorte la esconde)
                double x = (n * 61 + _reloj * (3 + n * 2)) % (Ancho + 16) - 16;
                l.Sprite(SpritesMundo.Nube, x, 1 + n * 8);
            }
    }

    // Lo que hace reconocible a cada zona, dibujado con rectangulos en las filas libres (4-5 y 10-11)
    void DibujarEscenario(Lienzo l, Escenario e)
    {
        const int arriba = FilasCielo * Tile;          // y 32: primera fila de suelo
        const int abajo = (FilaCamino + 2) * Tile;     // y 80: debajo del camino
        switch (e)
        {
            case Escenario.Aldea:
                foreach (int x in new[] { 18, 70, 122 })
                {
                    l.Relleno('U', x, arriba + 6, 16, 10);              // pared
                    l.Relleno('K', x + 6, arriba + 10, 4, 6);           // puerta
                    for (int f = 0; f < 6; f++) l.Relleno('P', x - 2 + f, arriba + 6 - f, 20 - f * 2, 1);  // techo
                }
                break;
            case Escenario.Pradera:
                // Flores en cruz: petalos claros y centro del color de logro
                for (int i = 0; i < 22; i++)
                {
                    double x = (i * 37) % (Ancho - 4) + 1, y = arriba + 2 + (i * 11) % 13;
                    l.Relleno(i % 2 == 0 ? 'W' : 'D', x - 1, y, 3, 1);
                    l.Relleno(i % 2 == 0 ? 'W' : 'D', x, y - 1, 1, 3);
                    l.Relleno('L', x, y, 1, 1);
                }
                break;
            case Escenario.Rio:
                l.Relleno('A', 0, abajo, Ancho, Alto - abajo);
                for (int x = 0; x < Ancho; x += 8)
                    l.Relleno('W', (x + _reloj * 6) % Ancho, abajo + 4 + (x / 8 % 2) * 6, 4, 1);
                break;
            case Escenario.Torre:
                l.Relleno('U', 116, 2, 20, arriba + 14);
                for (int x = 116; x < 136; x += 6) l.Relleno('U', x, 0, 4, 2);
                for (int y = 8; y < arriba + 10; y += 10) l.Relleno('K', 124, y, 3, 5);
                break;
            case Escenario.Mercado:
                foreach (int x in new[] { 14, 64, 114 })
                {
                    l.Relleno('U', x, arriba + 6, 2, 10);
                    l.Relleno('U', x + 22, arriba + 6, 2, 10);
                    for (int r = 0; r < 6; r++) l.Relleno(r % 2 == 0 ? 'P' : 'W', x + r * 4, arriba + 2, 4, 5);
                    l.Relleno('T', x + 4, arriba + 12, 16, 4);          // mercaderia
                }
                break;
            case Escenario.Castillo:
                l.Relleno('U', 0, arriba + 2, Ancho, 14);
                for (int x = 0; x < Ancho; x += 8) l.Relleno('U', x, arriba - 2, 4, 4);
                for (int x = 0; x < Ancho; x += 8) l.Relleno('K', x + (x / 8 % 2) * 4, arriba + 8, 1, 4);
                l.Relleno('K', 0, arriba + 7, Ancho, 1);
                break;
            case Escenario.Gremio:
                foreach (int x in new[] { 20, 56, 92, 128 })
                {
                    l.Relleno('K', x + 4, arriba, 1, 18);               // asta
                    l.Relleno('P', x - 2, arriba + 2, 12, 10);          // estandarte
                    l.Relleno('L', x + 2, arriba + 5, 4, 4);            // emblema
                }
                break;
            case Escenario.Guarida:
                l.Velo(0, arriba, Ancho, Alto - arriba, 0.35);
                break;
        }
    }

    // Cada golpe dura medio segundo: el heroe salta hacia el jefe y vuelve
    (double X, double Y) SaltoDePelea(double x, double y)
    {
        if (!Peleando) return (x, y);
        double inicio = Golpes.LastOrDefault(t => _tPelea >= t, -1);
        if (inicio < 0 || _tPelea >= inicio + 0.5) return (x, y);
        double ida = Math.Sin((_tPelea - inicio) / 0.5 * Math.PI);
        var (jx, jy) = PosJefe();
        return (x + (jx - 6 - x) * ida, y + (jy + 6 - y) * ida - 6 * ida);
    }

    void DibujarJefe(Lienzo l)
    {
        bool peleando = _tPelea >= 0;
        if (!JefeVisible || (peleando && _jefeExploto)) return;
        var (jx, jy) = PosJefe();

        if (peleando)
        {
            // Barra de vida: cada golpe le saca un tercio al llegar el heroe (mitad del salto)
            int recibidos = Golpes.Count(t => _tPelea >= t + 0.25);
            for (int i = 0; i < Golpes.Length; i++)
                l.Relleno(i < Golpes.Length - recibidos ? 'D' : 'K', jx + 1 + i * 5, jy - 4, 4, 2);
            bool parpadea = Golpes.Any(t => _tPelea >= t + 0.25 && _tPelea < t + 0.55) && (int)(_tPelea * 20) % 2 == 0;
            if (parpadea) return;
        }
        l.Sprite(SpritesMundo.Jefe, jx, jy);
    }

    void DibujarMapa(Lienzo l)
    {
        string tema = TemaService.Actual.ToString();
        l.Suelo(0, 0, Ancho, Alto);
        for (int i = 0; i + 1 < NodosMapa.Length; i++)
        {
            var (a, b) = (NodosMapa[i], NodosMapa[i + 1]);
            double x0 = Math.Min(a.Col, b.Col) * Tile, y0 = Math.Min(a.Fila, b.Fila) * Tile;
            l.Camino(x0, y0, (Math.Abs(a.Col - b.Col) + 1) * Tile, (Math.Abs(a.Fila - b.Fila) + 1) * Tile);
        }

        for (int i = 0; i < NodosMapa.Length; i++)
        {
            var (col, fila) = NodosMapa[i];
            l.Texto((i + 1).ToString(), col * Tile + 1, fila * Tile - 9, 7, 'W');
            l.Sprite(i < PosicionMapa ? SpritesMundo.MiniBandera
                : i == NodosMapa.Length - 1 ? SpritesMundo.Fantasma
                : SpritesMundo.MiniPuerta, col * Tile, fila * Tile);
        }

        // El heroe camina por el camino entre nodos, no salta de uno a otro
        double pos = Math.Clamp(_posMapa, 0, NodosMapa.Length - 1);
        int desde = (int)Math.Floor(pos);
        int hasta = Math.Min(desde + 1, NodosMapa.Length - 1);
        double f = pos - desde;
        double hx = NodosMapa[desde].Col + (NodosMapa[hasta].Col - NodosMapa[desde].Col) * f;
        double hy = NodosMapa[desde].Fila + (NodosMapa[hasta].Fila - NodosMapa[desde].Fila) * f;
        int cuadro = CaminandoMapa ? (int)(_reloj / SegundosPorCuadro) % 2 : 0;
        double rebote = CaminandoMapa ? cuadro : (int)(_reloj / 0.5) % 2;
        l.Sprite(Heroe(tema)[cuadro], hx * Tile - 4, hy * Tile - 8 - rebote);
    }

    void DibujarCartel(Lienzo l)
    {
        if (_tCartel >= DuracionCartel || string.IsNullOrEmpty(Cartel)) return;
        // Baja en 0.25 s, se queda y sube en 0.25 s
        // La caja crece con el texto: con fuentes anchas un cartel largo ocupa dos lineas
        var texto = l.Formatear(Cartel, 6, 'W', Ancho - 20);
        double alto = Math.Ceiling(texto.Height / l.U) + 10;
        double entra = Math.Min(1, _tCartel / 0.25), sale = Math.Min(1, (DuracionCartel - _tCartel) / 0.25);
        double y = -alto - 2 + (alto + 6) * Math.Min(entra, sale);
        l.Caja(6, y, Ancho - 12, alto);
        l.Dibujar(texto, 10, y + 5);
    }

    void DibujarMensajeFinal(Lienzo l)
    {
        var texto = l.Formatear(MensajeFinal, 7, 'W', Ancho - 36);
        double alto = Math.Ceiling(texto.Height / l.U) + 12;
        double y = Math.Round((Alto - alto) / 2);
        l.Caja(12, y, Ancho - 24, alto);
        l.Dibujar(texto, 18, y + 6);
    }

    static string[][] Heroe(string tema) => SpritesMundo.Heroe.GetValueOrDefault(tema) ?? SpritesMundo.Heroe["Win95"];

    static Dictionary<char, IBrush> LeerPaleta()
    {
        IBrush Color(string clave, IBrush defecto) =>
            Application.Current?.FindResource($"TemaJuego{clave}") as IBrush ?? defecto;

        return new()
        {
            ['K'] = Color("Contorno", Brushes.Black),
            ['P'] = Color("Principal", Brushes.Navy),
            ['S'] = Color("Piel", Brushes.Silver),
            ['D'] = Color("Detalle", Brushes.Red),
            ['W'] = Color("Brillo", Brushes.White),
            ['T'] = Color("Decoracion", Brushes.Gold),
            ['U'] = Color("Puerta", Brushes.Gray),
            ['G'] = Color("Suelo", Brushes.Teal),
            ['R'] = Color("Camino", Brushes.Silver),
            ['C'] = Color("Cielo", Brushes.SkyBlue),
            ['L'] = Color("Logro", Brushes.Gold),
            ['A'] = Color("Agua", Brushes.DodgerBlue),
        };
    }

    // Coordenadas en pixeles de juego (sin escalar); U = tamano de cada pixel en pantalla
    readonly record struct Lienzo(DrawingContext Ctx, Dictionary<char, IBrush> Paleta, double U, double Ox, double Oy)
    {
        static readonly IBrush Punto = new SolidColorBrush(Colors.Black, 0.12);
        static readonly IBrush Borde = new SolidColorBrush(Colors.Black, 0.25);
        static readonly IBrush FondoCaja = new SolidColorBrush(Colors.Black, 0.85);

        // Bordes redondeados a pixel de pantalla: sin costuras entre rectangulos vecinos con escala no entera
        Rect R(double x, double y, double w, double h)
        {
            x = Math.Round(x);
            y = Math.Round(y);
            double izq = Math.Round(Ox + x * U), arriba = Math.Round(Oy + y * U);
            return new Rect(izq, arriba, Math.Round(Ox + (x + w) * U) - izq, Math.Round(Oy + (y + h) * U) - arriba);
        }

        public void Relleno(char color, double x, double y, double w, double h)
        {
            if (Paleta.TryGetValue(color, out var pincel)) Ctx.FillRectangle(pincel, R(x, y, w, h));
        }

        public void Suelo(double x0, double y0, double w, double h)
        {
            Relleno('G', x0, y0, w, h);
            // Textura fija de puntitos para que el suelo no se vea plano
            for (double y = y0; y < y0 + h; y += 3)
                for (double x = x0 + (y * 5) % 7; x < x0 + w; x += 7)
                    Ctx.FillRectangle(Punto, R(x, y, 1, 1));
        }

        // Oscurece una zona (cielo de tormenta, suelo de la guarida)
        public void Velo(double x, double y, double w, double h, double opacidad) =>
            Ctx.FillRectangle(new SolidColorBrush(Colors.Black, opacidad), R(x, y, w, h));

        public void Circulo(char color, double cx, double cy, double radio)
        {
            for (double dy = -radio; dy <= radio; dy++)
            {
                double ancho = Math.Round(Math.Sqrt(radio * radio - dy * dy));
                Relleno(color, cx - ancho, cy + dy, ancho * 2, 1);
            }
        }

        public void Estrellas(double w, double h, double reloj)
        {
            for (int i = 0; i < 24; i++)
            {
                double x = (i * 37) % w, y = (i * 23) % h;
                // Titilan de a una, desfasadas
                if ((int)(reloj * 2 + i) % 5 != 0) Relleno('W', x, y, 1, 1);
            }
        }

        public void Camino(double x, double y, double w, double h)
        {
            Relleno('R', x, y, w, h);
            Ctx.FillRectangle(Borde, R(x, y, w, 1));
            Ctx.FillRectangle(Borde, R(x, y + h - 1, w, 1));
        }

        // Caja de dialogo retro: fondo oscuro con marco del color de detalle y linea interior clara
        public void Caja(double x, double y, double w, double h)
        {
            Ctx.FillRectangle(FondoCaja, R(x, y, w, h));
            Relleno('D', x, y, w, 1);
            Relleno('D', x, y + h - 1, w, 1);
            Relleno('D', x, y, 1, h);
            Relleno('D', x + w - 1, y, 1, h);
            Relleno('W', x + 2, y + 2, w - 4, 1);
            Relleno('W', x + 2, y + h - 3, w - 4, 1);
        }

        // Junta los pixeles seguidos del mismo color en un solo rectangulo: menos llamadas por cuadro
        public void Sprite(string[] filas, double x, double y)
        {
            for (int f = 0; f < filas.Length; f++)
            {
                string fila = filas[f];
                int c = 0;
                while (c < fila.Length)
                {
                    char letra = fila[c];
                    int desde = c;
                    while (c < fila.Length && fila[c] == letra) c++;
                    if (letra != '.') Relleno(letra, x + desde, y + f, c - desde, 1);
                }
            }
        }

        // anchoMax > 0: texto centrado que salta de linea dentro de ese ancho (en pixeles de juego)
        public FormattedText Formatear(string texto, double tamano, char color, double anchoMax = 0)
        {
            var fuente = Application.Current?.FindResource("TemaFontFamily") as FontFamily ?? FontFamily.Default;
            var formateado = new FormattedText(texto, System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight, new Typeface(fuente, FontStyle.Normal, FontWeight.Bold), tamano * U, Paleta[color]);
            if (anchoMax > 0)
            {
                formateado.MaxTextWidth = anchoMax * U;
                formateado.TextAlignment = TextAlignment.Center;
            }
            return formateado;
        }

        public void Dibujar(FormattedText texto, double x, double y) => Ctx.DrawText(texto, new Point(Ox + x * U, Oy + y * U));

        public void Texto(string texto, double x, double y, double tamano, char color) => Dibujar(Formatear(texto, tamano, color), x, y);
    }
}
