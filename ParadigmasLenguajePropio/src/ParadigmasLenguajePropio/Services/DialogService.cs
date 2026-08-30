using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ParadigmasLenguajePropio.Services;

// Reemplaza DialogoModal.cs: en vez de Action<int> alElegir (fire-and-forget), expone Task async.
// Se muestra como overlay dentro de la misma ventana (no una Window hija) para conservar el look
// original de "fondo oscurecido con remaches sobre la misma ventana".
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
        ValorTexto = valorInicial;
        Botones.Clear();
        // "Cancelar" al final a propósito: Escape siempre cierra con el último botón (ver Elegir),
        // así que el orden acá es lo que decide qué hace Escape.
        Botones.Add("Aceptar");
        Botones.Add("Cancelar");
        IsVisible = true;

        _tcsPrompt = new TaskCompletionSource<string?>();
        return _tcsPrompt.Task;
    }

    [RelayCommand]
    private void ElegirBoton(string boton) => Elegir(Botones.IndexOf(boton));

    // llamado por click en un botón o por Escape (índice = Botones.Count - 1, la convención de "cancelar")
    public void Elegir(int indice)
    {
        if (!IsVisible) return;
        IsVisible = false;

        if (EsPrompt)
        {
            // "Aceptar" es siempre el primer botón (ver PromptTextAsync)
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
