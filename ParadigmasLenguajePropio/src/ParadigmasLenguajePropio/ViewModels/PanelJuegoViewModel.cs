using System.Collections.ObjectModel;
using System.Text.Json;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de juego: barra de mana, resultado del nivel con estrellas, monedas, pistas y pausa
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

    // carpetaNivel = null cuando se ejecuta un .ggez que no es nivel
    public void ReiniciarMana(string? carpetaNivel)
    {
        Progreso = 1f;
        ResumenMana = "Ejecutando...";
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
        var partes = evento.Split(' ', 6);
        if (partes is ["pasos", var u, var l] && long.TryParse(u, out long usados) && long.TryParse(l, out long limite))
            MostrarMana(usados, limite);
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
