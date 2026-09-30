using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

// Reserva de espacio del panel de juego con progreso/pausa de ejemplo
public partial class PanelJuegoViewModel : ViewModelBase
{
    const int CantidadSegmentos = 24;

    [ObservableProperty]
    private bool _enPausa = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Segmentos))]
    private float _progreso = 0.55f;

    public IReadOnlyList<bool> Segmentos
    {
        get
        {
            int llenos = (int)(CantidadSegmentos * Progreso);
            var segmentos = new bool[CantidadSegmentos];
            for (int i = 0; i < llenos; i++) segmentos[i] = true;
            return segmentos;
        }
    }
}
