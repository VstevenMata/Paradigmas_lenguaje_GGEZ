using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

// Reemplaza PanelJuego.cs (la vista del juego en si sigue "sin diseñar", tal como estaba en el
// original - esto es la misma reserva de espacio con progreso/pausa de ejemplo). La animacion de
// colapso a mano con GetFrameTime() se saca de aca: la visibilidad/ancho del panel derecho ya la
// anima MainWindowViewModel, no hace falta que este ViewModel sepa de eso.
public partial class PanelJuegoViewModel : ViewModelBase
{
    // misma cantidad que SegmentosNivel en el original: la barra son segmentos discretos, no una
    // barra continua
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
