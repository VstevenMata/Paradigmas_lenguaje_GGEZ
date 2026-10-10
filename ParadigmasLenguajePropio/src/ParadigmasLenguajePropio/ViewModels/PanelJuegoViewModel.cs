using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de juego: barra de mana, resultado del nivel con estrellas y pausa
public partial class PanelJuegoViewModel : ViewModelBase
{
    const int CantidadSegmentos = 24;
    const int MaxEstrellas = 3;

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

    // ponytail: intentos y records en memoria, la fase 3 los guarda en disco
    readonly Dictionary<string, int> _intentos = new();
    readonly Dictionary<string, int> _records = new();
    string? _carpetaNivel;

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
        _carpetaNivel = carpetaNivel;
        if (carpetaNivel == null) EnNivel = false;
        else ResumenPruebas = "Corriendo pruebas...";
    }

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
        string clave = _carpetaNivel ?? titulo;
        int intento = _intentos[clave] = _intentos.GetValueOrDefault(clave) + 1;

        int estrellas = pasadas < total ? 0
            : 1 + (intento == 1 ? 1 : 0) + (meta == 0 || pasos <= meta ? 1 : 0);
        int record = _records[clave] = Math.Max(estrellas, _records.GetValueOrDefault(clave));

        EnNivel = true;
        TituloNivel = titulo;
        ResumenPruebas = $"Pruebas: {pasadas} de {total} · intento {intento}";
        Estrellas = new string('★', estrellas) + new string('☆', MaxEstrellas - estrellas);
        DetalleEstrellas = pasadas < total
            ? "Pase todas las pruebas para ganar estrellas"
            : $"{estrellas} de {MaxEstrellas} estrellas" + (record > estrellas ? $" · récord {record}" : "")
              + (meta > 0 && pasos > meta ? $" · meta: {meta:N0} pasos, usó {pasos:N0}" : "");
    }
}
