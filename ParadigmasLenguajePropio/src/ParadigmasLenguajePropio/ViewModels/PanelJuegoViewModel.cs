using System.Collections.ObjectModel;
using System.Text.Json;
using System.Text.RegularExpressions;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de juego: mundo de zonas, barra de mana, resultado del nivel con estrellas, monedas, pistas, logros y pausa
public partial class PanelJuegoViewModel : ViewModelBase
{
    const int CantidadSegmentos = 24;
    const int MaxEstrellas = 3;
    const int CostoPista = 1;
    const int IntentosParaSugerirPista = 5;

    [ObservableProperty]
    private bool _enPausa = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Segmentos))]
    private float _progreso = 1f;

    [ObservableProperty]
    private string _resumenMana = "Ejecute un .ggez (F5)";

    [ObservableProperty]
    private bool _enNivel;

    [ObservableProperty]
    private string _tituloNivel = "";

    [ObservableProperty]
    private string _resumenPruebas = "";

    [ObservableProperty]
    private string _estrellas = "";

    [ObservableProperty]
    private string _detalleEstrellas = "";

    [ObservableProperty]
    private string _resumenMonedas = "";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayPistas), nameof(QuedanPistas), nameof(TextoComprarPista))]
    [NotifyCanExecuteChangedFor(nameof(ComprarPistaCommand))]
    private int _pistasRestantes;

    public ObservableCollection<string> PistasCompradas { get; } = new();

    // Aviso de logros, misiones y zonas ganados en la ultima ejecucion; se limpia al correr otra vez
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayAviso))]
    private string _aviso = "";

    public bool HayAviso => Aviso.Length > 0;

    // Mundo: la zona actual y sus misiones; lo dibuja Controls/MundoPixel
    [ObservableProperty]
    private int _zonaMundo;

    // Tramos recorridos en la zona (fraccion por mision a medias); TotalZona + 1 = en la puerta
    [ObservableProperty]
    private double _avanceZona;

    [ObservableProperty]
    private int _totalZona;

    // Bit k = mision k completa (su bandera se pinta lograda)
    [ObservableProperty]
    private int _banderasLogradas;

    // Nodo del mapa donde esta el heroe
    [ObservableProperty]
    private int _posicionMapa;

    [ObservableProperty]
    private bool _puertaAbierta;

    [ObservableProperty]
    private bool _jefeVisible;

    [ObservableProperty]
    private bool _peleaJefe;

    // Cartel que baja en la pantalla de arriba; los avisos se encolan y se muestran de a uno
    [ObservableProperty]
    private string _cartel = "";

    // Mensaje fijo al ganar el mundo; mientras esta, X reinicia y A cierra
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HayMensajeFinal))]
    private string _mensajeFinal = "";

    public bool HayMensajeFinal => MensajeFinal.Length > 0;

    [ObservableProperty]
    private int _sacudidas;

    [ObservableProperty]
    private bool _verMapa;

    // false al mirar con la cruceta una zona que no es la actual
    [ObservableProperty]
    private bool _mostrarHeroe = true;

    [ObservableProperty]
    private string _tituloZona = "";

    [ObservableProperty]
    private string _estadoZona = "";

    // -1 = seguir la zona actual; si no, la que se eligio con la cruceta
    int _zonaVista = -1;

    public ObservableCollection<string> MisionesZona { get; } = new();

    static readonly Regex HuellaValida = new("^[0-9a-f]{8,64}$", RegexOptions.Compiled);
    const double SegundosPorCartel = 2.5;
    // Mientras corre una secuencia (puerta, mapa, pelea) nada la interrumpe
    bool _enSecuencia;
    // Pendientes por tipo: los logros de una misma corrida se juntan en un solo cartel
    readonly List<(string Tipo, List<string> Textos)> _carteles = new();
    bool _mostrandoCartel;

    [ObservableProperty]
    private bool _mostrarLogros;

    [ObservableProperty]
    private string _resumenLogros = "";

    // "[x]" / "[ ]" ademas del texto: no depende del color para saber cual se gano
    public ObservableCollection<string> ListaLogros { get; } = new();

    readonly List<string> _avisos = new();

    public bool HayPistas => PistasRestantes > 0 || PistasCompradas.Count > 0;

    public bool QuedanPistas => PistasRestantes > 0;

    public string TextoComprarPista => $"Comprar pista ({CostoPista} moneda, quedan {PistasRestantes})";

    // _carpetaNivel = nivel del archivo enfocado (lo que se muestra y donde se compran pistas)
    // _carpetaEjecutada = nivel que corrio el ultimo F5 (a quien se le anotan los intentos)
    string? _carpetaNivel;
    string? _carpetaEjecutada;
    IReadOnlyList<string> _pistas = [];

    public IReadOnlyList<bool> Segmentos
    {
        get
        {
            // Ceiling: con algo de mana restante queda al menos un segmento, vacio solo al agotarse
            int llenos = (int)Math.Ceiling(CantidadSegmentos * Progreso);
            var segmentos = new bool[CantidadSegmentos];
            for (int i = 0; i < llenos; i++) segmentos[i] = true;
            return segmentos;
        }
    }

    public PanelJuegoViewModel()
    {
        RefrescarLogros();
        RefrescarMundo();
    }

    // Botones de la consola: A mapa, B volver, X logros, Y pausa; la cruceta recorre las zonas
    // Con el mensaje final: X reinicia el mundo y A lo cierra
    [RelayCommand]
    void AlternarMapa()
    {
        if (HayMensajeFinal) { MensajeFinal = ""; return; }
        if (!_enSecuencia) VerMapa = !VerMapa;
    }

    [RelayCommand]
    void VerZona()
    {
        if (!_enSecuencia) VerMapa = false;
    }

    [RelayCommand]
    void VolverAZonaActual()
    {
        if (_enSecuencia) return;
        VerMapa = false;
        _zonaVista = -1;
        RefrescarMundo();
    }

    [RelayCommand]
    void AlternarPausa() => EnPausa = !EnPausa;

    [RelayCommand]
    void ZonaAnterior() => MirarZona(-1);

    [RelayCommand]
    void ZonaSiguiente() => MirarZona(+1);

    void MirarZona(int paso)
    {
        if (_enSecuencia) return;
        int actual = Math.Min(ProgresoService.ZonaActual, Models.Mundo.Zonas.Count - 1);
        int vista = Math.Clamp((_zonaVista < 0 ? actual : _zonaVista) + paso, 0, Models.Mundo.Zonas.Count - 1);
        _zonaVista = vista == actual ? -1 : vista;
        VerMapa = false;
        RefrescarMundo();
    }

    [RelayCommand]
    void AlternarLogros()
    {
        if (HayMensajeFinal) ReiniciarJuego();
        else MostrarLogros = !MostrarLogros;
    }

    void ReiniciarJuego()
    {
        ProgresoService.ReiniciarMundo();
        ProgresoService.Guardar();
        MensajeFinal = "";
        _zonaVista = -1;
        VerMapa = false;
        RefrescarMundo();
        EncolarCartel("¡Mundo reiniciado! Zona 1: Aldea");
    }

    // carpetaNivel = null cuando se ejecuta un .ggez que no es nivel
    public void ReiniciarMana(string? carpetaNivel)
    {
        Progreso = 1f;
        ResumenMana = "Ejecutando...";
        _avisos.Clear();
        Aviso = "";
        _carpetaEjecutada = carpetaNivel;
        if (carpetaNivel != null && carpetaNivel == _carpetaNivel) ResumenPruebas = "Corriendo pruebas...";
    }

    // Sigue al archivo enfocado: muestra su nivel guardado sin tener que correrlo
    public void EnfocarArchivo(string? ruta)
    {
        string? carpeta = Path.GetDirectoryName(ruta ?? "");
        if (string.IsNullOrEmpty(carpeta) || !File.Exists(Path.Combine(carpeta, "nivel.json")))
        {
            _carpetaNivel = null;
            EnNivel = false;
            return;
        }
        if (carpeta == _carpetaNivel) return;

        _carpetaNivel = carpeta;
        (string titulo, _pistas) = LeerNivel(carpeta);
        var nivel = ProgresoService.Nivel(carpeta);
        EnNivel = true;
        TituloNivel = titulo;
        ResumenPruebas = nivel.Intentos == 0 ? "Sin intentar · F5 para comprobar" : $"Intentos: {nivel.Intentos}";
        Estrellas = TextoEstrellas(nivel.Record);
        DetalleEstrellas = nivel.Intentos == 0 ? "" : $"Récord: {nivel.Record} de {MaxEstrellas} estrellas";
        MostrarPistas();
    }

    static string TextoEstrellas(int estrellas) => new string('★', estrellas) + new string('☆', MaxEstrellas - estrellas);

    // Eventos del traductor sin el prefijo "@ggez "; los que no conoce se ignoran
    public void RecibirEvento(string evento)
    {
        if (evento == "fallo")
        {
            Sacudidas++;
            return;
        }
        if (evento.StartsWith("programa "))
        {
            var datos = evento.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            if (datos.Length >= 2 && HuellaValida.IsMatch(datos[1])) ProgramaCorrido(datos[1], datos[2..].ToHashSet());
            return;
        }

        var partes = evento.Split(' ', 6);
        if (partes is ["pasos", var u, var l] && long.TryParse(u, out long usados) && long.TryParse(l, out long limite))
            MostrarMana(usados, limite);
        else if (partes is ["logro", var id])
            GanarLogro(id);
        else if (partes is ["nivel", var p, var t, var pa, var m, ..] && int.TryParse(p, out int pasadas) && int.TryParse(t, out int total)
                 && long.TryParse(pa, out long pasos) && long.TryParse(m, out long meta))
            MostrarNivel(pasadas, total, pasos, meta, partes.Length > 5 ? partes[5] : "");
    }

    void MostrarMana(long usados, long limite)
    {
        if (limite <= 0)
        {
            Progreso = 1f;
            ResumenMana = $"{usados:N0} pasos (sin límite)";
            return;
        }

        Progreso = Math.Clamp(1f - (float)usados / limite, 0f, 1f);
        ResumenMana = usados > limite
            ? "¡Sin maná! Revise los ciclos"
            : $"{usados:N0} de {limite:N0} pasos";
        if (usados > limite) GanarLogro("sin-mana");
    }

    // Estrellas: 1 por pasar todo, +1 si fue al primer intento, +1 si no paso de la meta de pasos
    void MostrarNivel(int pasadas, int total, long pasos, long meta, string titulo)
    {
        if (_carpetaEjecutada == null) return;
        var nivel = ProgresoService.Nivel(_carpetaEjecutada);
        nivel.Intentos++;

        int estrellas = pasadas < total ? 0
            : 1 + (nivel.Intentos == 1 ? 1 : 0) + (meta == 0 || pasos <= meta ? 1 : 0);
        nivel.Record = Math.Max(estrellas, nivel.Record);
        ProgresoService.Guardar();
        if (estrellas == MaxEstrellas) GanarLogro("nivel-perfecto");

        // Si mientras corria se enfoco otro archivo, el resultado se guarda pero no pisa lo que se ve
        if (_carpetaEjecutada != _carpetaNivel)
        {
            MostrarPistas();
            return;
        }

        EnNivel = true;
        TituloNivel = titulo;
        ResumenPruebas = $"Pruebas: {pasadas} de {total} · intento {nivel.Intentos}";
        Estrellas = TextoEstrellas(estrellas);
        DetalleEstrellas = pasadas < total
            ? "Pase todas las pruebas para ganar estrellas"
              + (nivel.Intentos >= IntentosParaSugerirPista && PistasRestantes > 0 ? ". ¿Atascado? Compre una pista" : "")
            : $"{estrellas} de {MaxEstrellas} estrellas" + (nivel.Record > estrellas ? $" · récord {nivel.Record}" : "")
              + (meta > 0 && pasos > meta ? $" · meta: {meta:N0} pasos, usó {pasos:N0}" : "");
        MostrarPistas();
    }

    bool PuedeComprarPista() => PistasRestantes > 0 && ProgresoService.Monedas >= CostoPista;

    [RelayCommand(CanExecute = nameof(PuedeComprarPista))]
    void ComprarPista()
    {
        if (_carpetaNivel == null || !ProgresoService.Gastar(CostoPista)) return;
        ProgresoService.Nivel(_carpetaNivel).PistasCompradas++;
        ProgresoService.Guardar();
        MostrarPistas();
        GanarLogro("primera-pista");
    }

    void MostrarPistas()
    {
        int compradas = _carpetaNivel == null ? 0 : Math.Min(ProgresoService.Nivel(_carpetaNivel).PistasCompradas, _pistas.Count);
        PistasCompradas.Clear();
        foreach (var pista in _pistas.Take(compradas)) PistasCompradas.Add(pista);
        PistasRestantes = _pistas.Count - compradas;
        ResumenMonedas = $"Total: {ProgresoService.EstrellasTotales} ★ · {ProgresoService.Monedas} monedas";
        OnPropertyChanged(nameof(HayPistas));
        // Las monedas pueden cambiar sin que cambien las pistas restantes
        ComprarPistaCommand.NotifyCanExecuteChanged();
    }

    void GanarLogro(string id)
    {
        if (Logros.Buscar(id) is not { } logro || !ProgresoService.GanarLogro(id)) return;
        ProgresoService.Guardar();
        Avisar($"Logro: {logro.Nombre}");
        EncolarCartel(logro.Nombre, "logro");
        RefrescarLogros();
    }

    // tipo "logro" o "avance": se suma al ultimo pendiente del mismo tipo en vez de hacer cola
    void EncolarCartel(string texto, string tipo = "")
    {
        if (tipo != "" && _carteles.Count > 0 && _carteles[^1].Tipo == tipo) _carteles[^1].Textos.Add(texto);
        else _carteles.Add((tipo, [texto]));
        if (!_mostrandoCartel) SiguienteCartel();
    }

    // Para los momentos de la secuencia (jefe derrotado, final): se muestra ya y lo pendiente se descarta
    void CartelAhora(string texto)
    {
        _carteles.Clear();
        _carteles.Add(("", [texto]));
        _versionCartel++;
        SiguienteCartel();
    }

    int _versionCartel;

    void SiguienteCartel()
    {
        if (_carteles.Count == 0)
        {
            _mostrandoCartel = false;
            return;
        }
        var (tipo, textos) = _carteles[0];
        _carteles.RemoveAt(0);
        _mostrandoCartel = true;
        string texto = tipo switch
        {
            "logro" => (textos.Count > 1 ? "¡Logros! " : "¡Logro! ") + string.Join(" · ", textos),
            "avance" => textos.Count > 1 ? $"¡Avance en {textos.Count} misiones!" : textos[0],
            _ => textos[0],
        };
        // Vacio y despues el texto: dos carteles iguales seguidos tambien se vuelven a mostrar
        Cartel = "";
        Cartel = texto;
        int version = _versionCartel;
        // Si mientras tanto llego un CartelAhora, este temporizador ya no manda
        Despues(SegundosPorCartel, () => { if (version == _versionCartel) SiguienteCartel(); });
    }

    static void Despues(double segundos, Action accion) => DispatcherTimer.RunOnce(accion, TimeSpan.FromSeconds(segundos));

    void Avisar(string texto)
    {
        _avisos.Add(texto);
        Aviso = $"¡{string.Join(" · ", _avisos)}!";
    }

    // Un programa que corrio bien: cuenta para las misiones que cumple y el heroe camina
    void ProgramaCorrido(string huella, IReadOnlySet<string> usos)
    {
        if (_enSecuencia) return;
        int zonaAntes = ProgresoService.ZonaActual;
        var avanzadas = ProgresoService.RegistrarPrograma(huella, usos);
        if (avanzadas.Count == 0) return;
        ProgresoService.Guardar();
        // Vuelve a la zona actual para ver caminar al heroe
        _zonaVista = -1;
        VerMapa = false;
        foreach (var mision in avanzadas)
        {
            bool completa = ProgresoService.MisionCompleta(mision);
            if (completa) Avisar($"Misión: {mision.Texto}");
            if (completa) EncolarCartel($"¡Misión cumplida! {mision.Texto}");
            else EncolarCartel($"¡Avance! {mision.Texto} ({ProgresoService.Cuenta(mision)}/{mision.Veces})", "avance");
        }

        var zonas = Models.Mundo.Zonas;
        if (ProgresoService.ZonaActual == zonaAntes || zonaAntes >= zonas.Count)
        {
            RefrescarMundo();
            return;
        }

        // Zona ganada: banderas prendidas y el heroe va recto, sin pararse en cada bandera
        _enSecuencia = true;
        RefrescarMisiones(zonas[zonaAntes]);
        BanderasLogradas = (1 << TotalZona) - 1;
        EncolarCartel($"¡Zona completada! {zonas[zonaAntes].Nombre} (+{Models.Mundo.EstrellasPorZona} ★)");
        MostrarPistas();

        if (zonaAntes == zonas.Count - 1) PelearConJefe();
        else CruzarPuerta(() => CaminarEnMapa(zonaAntes + 1));
    }

    double SegundosHasta(double destino) => Math.Abs(destino - AvanceZona) * 0.45 + 0.4;

    void CruzarPuerta(Action despues)
    {
        PuertaAbierta = true;
        double espera = SegundosHasta(TotalZona + 1);
        AvanceZona = TotalZona + 1;
        Despues(espera, despues);
    }

    // En el mapa camina del nodo de la zona ganada al de la siguiente y recien ahi entra a la zona nueva
    void CaminarEnMapa(int zonaNueva)
    {
        VerMapa = true;
        PosicionMapa = zonaNueva;
        Despues(1.4, () =>
        {
            _enSecuencia = false;
            VerMapa = false;
            RefrescarMundo();
        });
    }

    // Ultima zona: el heroe llega frente al jefe, pelea, la puerta se abre, la cruza y sale el mensaje final
    void PelearConJefe()
    {
        double espera = SegundosHasta(TotalZona);
        AvanceZona = TotalZona;
        Despues(espera, () =>
        {
            PeleaJefe = true;
            Despues(Controls.MundoPixel.DuracionPelea + 0.2, () =>
            {
                PeleaJefe = false;
                JefeVisible = false;
                CartelAhora("¡Jefe derrotado!");
                CruzarPuerta(() =>
                {
                    // Entro por la puerta: ya no se ve; el cartel que quede se apaga para no tapar el mensaje
                    MostrarHeroe = false;
                    _carteles.Clear();
                    _versionCartel++;
                    _mostrandoCartel = false;
                    Cartel = "";
                    _enSecuencia = false;
                    PosicionMapa = Models.Mundo.Zonas.Count - 1;
                    EstadoZona = "¡Mundo completado!";
                    MensajeFinal = "¡GANASTE EL MUNDO GGEZ!\nX: reiniciar el juego\nA: salir";
                });
            });
        });
    }

    void RefrescarMundo()
    {
        if (_enSecuencia) return;
        var zonas = Models.Mundo.Zonas;
        int actual = ProgresoService.ZonaActual;
        int indiceActual = Math.Min(actual, zonas.Count - 1);
        int indice = _zonaVista < 0 ? indiceActual : _zonaVista;
        var zona = zonas[indice];
        bool hecha = ProgresoService.ZonaCompleta(zona);

        PosicionMapa = indiceActual;
        TotalZona = zona.Misiones.Count;
        MostrarHeroe = indice == indiceActual;
        PuertaAbierta = hecha;
        JefeVisible = indice == zonas.Count - 1 && !hecha;
        BanderasLogradas = zona.Misiones.Select((m, i) => ProgresoService.MisionCompleta(m) ? 1 << i : 0).Sum();
        // Antes que la zona: si la zona cambia, el heroe aparece en su lugar en vez de caminar
        AvanceZona = hecha ? TotalZona + 1 : ProgresoService.Avance(zona);
        ZonaMundo = indice;
        TituloZona = $"Zona {indice + 1}/{zonas.Count} · {zona.Nombre}";
        EstadoZona = actual >= zonas.Count ? "¡Mundo completado!"
            : indice < actual ? $"Completada (+{Models.Mundo.EstrellasPorZona} ★)"
            : indice == actual ? $"Zona actual · {ProgresoService.EstrellasTotales} ★"
            : "Más adelante: lo que hagas ya cuenta";

        RefrescarMisiones(zona);
        MostrarPistas();
    }

    void RefrescarMisiones(Zona zona)
    {
        MisionesZona.Clear();
        foreach (var mision in zona.Misiones)
        {
            int cuenta = ProgresoService.Cuenta(mision);
            string veces = mision.Veces > 1 ? $" ({cuenta}/{mision.Veces})" : "";
            MisionesZona.Add($"{(cuenta >= mision.Veces ? "[x]" : "[ ]")} {mision.Texto}{veces}");
        }
    }

    void RefrescarLogros()
    {
        ListaLogros.Clear();
        foreach (var logro in Logros.Todos)
            ListaLogros.Add($"{(ProgresoService.TieneLogro(logro.Id) ? "[x]" : "[ ]")} {logro.Nombre}: {logro.Descripcion}");
        ResumenLogros = $"Logros: {Logros.Todos.Count(l => ProgresoService.TieneLogro(l.Id))} de {Logros.Todos.Count}";
    }

    // nivel.json puede venir de otra persona: solo se toman "titulo" y los textos de "pistas"
    static (string Titulo, IReadOnlyList<string> Pistas) LeerNivel(string carpeta)
    {
        string titulo = Path.GetFileName(carpeta);
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(carpeta, "nivel.json")));
            var raiz = doc.RootElement;
            if (raiz.ValueKind != JsonValueKind.Object) return (titulo, []);
            if (raiz.TryGetProperty("titulo", out var t) && t.ValueKind == JsonValueKind.String && t.GetString() is { Length: > 0 } texto)
                titulo = texto;
            if (raiz.TryGetProperty("pistas", out var pistas) && pistas.ValueKind == JsonValueKind.Array)
                return (titulo, pistas.EnumerateArray().Where(p => p.ValueKind == JsonValueKind.String).Select(p => p.GetString()!).ToList());
        }
        catch (Exception ex) when (ex is IOException or JsonException or UnauthorizedAccessException)
        {
            // Sin pistas: el traductor ya reporta el nivel.json roto
        }
        return (titulo, []);
    }
}
