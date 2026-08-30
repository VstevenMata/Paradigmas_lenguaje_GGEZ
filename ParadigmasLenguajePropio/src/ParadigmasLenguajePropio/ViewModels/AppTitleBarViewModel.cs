using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.ViewModels;

public partial class AppTitleBarViewModel : ViewModelBase
{
    // conectado por MainWindowViewModel en la Fase 4, cuando el explorador existe
    public Func<string, IEnumerable<ResultadoBusqueda>>? BuscarArchivos { get; set; }
    public Action<string>? AbrirResultado { get; set; }

    [ObservableProperty]
    private string _textoBusqueda = "";

    [ObservableProperty]
    private int _indiceSeleccionado = -1;

    public ObservableCollection<ResultadoBusqueda> Resultados { get; } = new();

    public bool ResultadosVisibles => Resultados.Count > 0;

    partial void OnTextoBusquedaChanged(string value)
    {
        Resultados.Clear();

        if (!string.IsNullOrWhiteSpace(value) && BuscarArchivos != null)
        {
            foreach (var r in BuscarArchivos.Invoke(value))
                Resultados.Add(r);
        }

        IndiceSeleccionado = Resultados.Count > 0 ? 0 : -1;
        OnPropertyChanged(nameof(ResultadosVisibles));
    }

    public void MoverSeleccion(int delta)
    {
        if (Resultados.Count == 0) return;
        IndiceSeleccionado = Math.Clamp(IndiceSeleccionado + delta, 0, Resultados.Count - 1);
    }

    public void AbrirSeleccionado()
    {
        if (IndiceSeleccionado < 0 || IndiceSeleccionado >= Resultados.Count) return;
        AbrirResultado?.Invoke(Resultados[IndiceSeleccionado].RutaCompleta);
        CerrarResultados();
    }

    public void AbrirResultadoEn(ResultadoBusqueda resultado)
    {
        AbrirResultado?.Invoke(resultado.RutaCompleta);
        CerrarResultados();
    }

    public void CerrarResultados()
    {
        TextoBusqueda = "";
        Resultados.Clear();
        IndiceSeleccionado = -1;
        OnPropertyChanged(nameof(ResultadosVisibles));
    }
}
