using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.Services;

// Dialogo modal como overlay dentro de la misma ventana, con API async
public partial class DialogService : ObservableObject
{
    [ObservableProperty]
    private bool _isVisible;

    [ObservableProperty]
    private string _titulo = "";

    [ObservableProperty]
    private string _mensaje = "";

    [ObservableProperty]
    private bool _esPrompt;

    // No null = dialogo de ayuda con secciones en vez de Mensaje
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsAyuda), nameof(EsMensaje))]
    private IReadOnlyList<SeccionAyuda>? _secciones;

    // Vista fija del "Acerca de" (logo, equipo, versiculo) en vez de Mensaje
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsMensaje))]
    private bool _esAcercaDe;

    [ObservableProperty]
    private string _subtitulo = "";

    public bool EsAyuda => Secciones != null;
    public bool EsMensaje => !EsAyuda && !EsAcercaDe;

    [ObservableProperty]
    private string _valorTexto = "";

    public ObservableCollection<string> Botones { get; } = new();

    TaskCompletionSource<int>? _tcsModal;
    TaskCompletionSource<string?>? _tcsPrompt;

    public Task<int> ShowModalAsync(string titulo, string mensaje, params string[] botones)
    {
        Titulo = titulo;
        Mensaje = mensaje;
        EsPrompt = false;
        Secciones = null;
        EsAcercaDe = false;
        Botones.Clear();
        foreach (var b in botones) Botones.Add(b);
        IsVisible = true;

        _tcsModal = new TaskCompletionSource<int>();
        return _tcsModal.Task;
    }

    public Task<string?> PromptTextAsync(string titulo, string mensaje, string valorInicial)
    {
        Titulo = titulo;
        Mensaje = mensaje;
        EsPrompt = true;
        Secciones = null;
        EsAcercaDe = false;
        ValorTexto = valorInicial;
        Botones.Clear();
        // "Cancelar" al final a proposito: Escape elige siempre el ultimo boton
        Botones.Add("Aceptar");
        Botones.Add("Cancelar");
        IsVisible = true;

        _tcsPrompt = new TaskCompletionSource<string?>();
        return _tcsPrompt.Task;
    }

    public Task<int> ShowAyudaAsync(string titulo, string subtitulo, IReadOnlyList<SeccionAyuda> secciones)
    {
        var tarea = ShowModalAsync(titulo, "", "Cerrar");
        Subtitulo = subtitulo;
        Secciones = secciones;
        return tarea;
    }

    public Task<int> ShowAcercaDeAsync()
    {
        var tarea = ShowModalAsync("Acerca de GGEZ", "", "Cerrar");
        EsAcercaDe = true;
        return tarea;
    }

    [RelayCommand]
    private void ElegirBoton(string boton) => Elegir(Botones.IndexOf(boton));

    public void Elegir(int indice)
    {
        if (!IsVisible) return;
        IsVisible = false;

        if (EsPrompt)
        {
            bool acepto = indice == 0;
            _tcsPrompt?.SetResult(acepto ? ValorTexto : null);
            _tcsPrompt = null;
        }
        else
        {
            _tcsModal?.SetResult(indice);
            _tcsModal = null;
        }
    }
}
