using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de chat de la extension "Claude para GGEZ" (ver MainWindow.axaml / MainWindowViewModel.ChatVisible):
// vive debajo de PanelJuegoView, en la misma columna, y solo puede haber una instancia abierta a la vez.
// PedirRespuestaAsync es el unico punto pendiente de conectar con la llamada real a la API.
public partial class ChatViewModel : ViewModelBase
{
    public ObservableCollection<ChatMensaje> Mensajes { get; } = new();

    [ObservableProperty]
    private string _entradaActual = "";

    [ObservableProperty]
    private bool _enviando;

    public Action? OnCerrar; // lo dispara el boton de cerrar del panel

    [RelayCommand]
    void Cerrar() => OnCerrar?.Invoke();

    [RelayCommand(CanExecute = nameof(PuedeEnviar))]
    async Task Enviar()
    {
        string texto = EntradaActual.Trim();
        if (texto.Length == 0) return;

        Mensajes.Add(new ChatMensaje(texto, DeUsuario: true));
        EntradaActual = "";

        Enviando = true;
        try
        {
            string respuesta = await PedirRespuestaAsync(texto);
            Mensajes.Add(new ChatMensaje(respuesta, DeUsuario: false));
        }
        finally
        {
            Enviando = false;
        }
    }

    bool PuedeEnviar() => !Enviando;
    partial void OnEnviandoChanged(bool value) => EnviarCommand.NotifyCanExecuteChanged();

    // placeholder: aca va la llamada real a la API de Claude (mandar Mensajes + el archivo activo
    // como contexto, y aplicar las ediciones que sugiera - ver descripcion de la extension)
    static Task<string> PedirRespuestaAsync(string _) =>
        Task.FromResult("(la conexion con la API de Claude todavia no esta implementada)");
}
