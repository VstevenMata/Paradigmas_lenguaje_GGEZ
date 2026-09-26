using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

// carga en el arrastre de una pestaña de terminal: solo la instancia (a diferencia de los tabs del
// editor no hace falta acordarse de "de que panel salio" - solo hay una lista plana de columnas)
record PayloadArrastreTerminal(TerminalInstanceViewModel Instancia);

public partial class TerminalView : UserControl
{
    static readonly DataFormat<PayloadArrastreTerminal> FormatoArrastreTerminal =
        DataFormat.CreateInProcessFormat<PayloadArrastreTerminal>("ggez/terminal-tab");

    const double UmbralArrastre = 5; // pixeles antes de que un click-y-mantener se considere arrastre

    TerminalViewModel? ViewModel => DataContext as TerminalViewModel;

    ScrollViewer _scrollErrores = null!;
    ItemsControl _columnas = null!;
    Border _lineaInsercion = null!;
    bool _erroresAncladoAlFinal = true;
    bool _scrollErroresPendiente;
    (TerminalInstanceViewModel Instancia, Point Inicio, PointerPressedEventArgs Disparador)? _candidatoArrastre;

    public TerminalView()
    {
        InitializeComponent();

        _scrollErrores = this.FindControl<ScrollViewer>("PART_ScrollErrores")!;
        _columnas = this.FindControl<ItemsControl>("PART_Columnas")!;
        _lineaInsercion = this.FindControl<Border>("PART_LineaInsercion")!;

        _scrollErrores.ScrollChanged += (_, _) =>
            _erroresAncladoAlFinal = _scrollErrores.Offset.Y >= _scrollErrores.Extent.Height - _scrollErrores.Viewport.Height - 2;

        DataContextChanged += (_, _) =>
        {
            if (ViewModel is not { } vm) return;
            vm.LineasErrores.CollectionChanged += SeguirErroresAlFinalSiCorresponde;
        };

        DragDrop.SetAllowDrop(_columnas, true);
        DragDrop.AddDragOverHandler(_columnas, Columnas_DragOver);
        DragDrop.AddDragLeaveHandler(_columnas, (_, _) => _lineaInsercion.IsVisible = false);
        DragDrop.AddDropHandler(_columnas, Columnas_Drop);

        _columnas.ContainerPrepared += Columnas_ContainerPrepared;
    }

    // el ItemsControl arma un contenedor (ContentPresenter) por cada item de Instancias, ocultas
    // incluidas (ver comentario en TerminalView.axaml) - lo unico que decide si esa columna se ve o
    // no es este enlace de IsVisible a EnColumna, atado a mano en vez de por un Style porque los
    // estilos de Avalonia cascadean por TODO el arbol visual descendiente (no solo dentro de este
    // ItemsControl como en WPF): un Selector="ContentPresenter" pisaria tambien el ContentPresenter
    // interno de cada Win95Bevel/ScrollViewer/Boton de la columna. Enlazando directo sobre el
    // contenedor que entrega el evento no hay ambiguedad posible.
    void Columnas_ContainerPrepared(object? sender, ContainerPreparedEventArgs e) =>
        e.Container.Bind(Visual.IsVisibleProperty, new Binding(nameof(TerminalInstanceViewModel.EnColumna)));

    void SeguirErroresAlFinalSiCorresponde(object? sender, NotifyCollectionChangedEventArgs e)
    {
        // un comando ruidoso agrega cientos de lineas por tick: un solo ScrollToEnd alcanza
        if (!_erroresAncladoAlFinal || _scrollErroresPendiente) return;
        _scrollErroresPendiente = true;
        Dispatcher.UIThread.Post(() => { _scrollErroresPendiente = false; _scrollErrores.ScrollToEnd(); }, DispatcherPriority.Background);
    }

    void TabErrores_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is { } vm) vm.TabActiva = 1;
    }

    void TabTerminal_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is { } vm) vm.TabActiva = 0;
    }

    void TabProblemas_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is { } vm) vm.TabActiva = 2;
    }

    // click simple en un chip oculto REEMPLAZA a la activa (ver MostrarEnColumna); arrastrarlo en
    // vez de solo clickear lo suelta justo donde se quiera (ver Pestana_PointerMoved/Columnas_Drop) -
    // mismo patron "click reemplaza, arrastrar agrupa/reubica" que las pestañas de columna.
    // OJO: a diferencia de Pestana_PointerPressed, el comando de este chip (MostrarEnColumnaCommand)
    // lo saca de InstanciasOcultas - ejecutarlo en PointerPressed destruiria el control bajo el
    // puntero antes de capturarlo, y el arrastre nunca llegaria a empezar. Por eso se difiere a
    // PointerReleased: si nunca se convierte en arrastre real (ver PointerMoved), ahi se trata como
    // el click simple que fue.
    void InstanciaOculta_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TerminalInstanceViewModel instancia }) return;
        _candidatoArrastre = (instancia, e.GetPosition(this), e);
        e.Pointer.Capture((IInputElement)sender);
    }

    void InstanciaOculta_PointerMoved(object? sender, PointerEventArgs e) => Pestana_PointerMoved(sender, e);

    void InstanciaOculta_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var candidato = _candidatoArrastre; // sigue seteado solo si no se convirtio en arrastre real
        _candidatoArrastre = null;
        e.Pointer.Capture(null);
        if (candidato is { } c && ViewModel is { } vm) vm.MostrarEnColumnaCommand.Execute(c.Instancia);
    }

    // ─────────────────────────── pestaña de columna: click / arrastre para reordenar o reubicar ───────────────────────────

    // click simple ya la activa (igual que clickear un tab de archivo cambia la pestaña activa);
    // recien al moverse mas de UmbralArrastre con el boton apretado se considera un arrastre real
    void Pestana_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TerminalInstanceViewModel instancia } || ViewModel is not { } vm) return;
        vm.EnfocarInstanciaCommand.Execute(instancia);
        _candidatoArrastre = (instancia, e.GetPosition(this), e);
        // captura explicita: sin esto, en algunas plataformas (Wayland/XWayland) el estado del
        // boton que reporta PointerMoved despues de moverse un poco deja de ser confiable, y el
        // arrastre se cancelaba solo antes de cruzar el umbral
        e.Pointer.Capture((IInputElement)sender);
    }

    void Pestana_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_candidatoArrastre is not { } candidato) return;
        double distancia = Point.Distance(candidato.Inicio, e.GetPosition(this));
        if (distancia < UmbralArrastre) return;

        _candidatoArrastre = null;
        e.Pointer.Capture(null); // DoDragDropAsync toma el control del puntero de aca en adelante
        _ = IniciarArrastreAsync(candidato.Instancia, candidato.Disparador);
    }

    void Pestana_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        _candidatoArrastre = null;
        e.Pointer.Capture(null);
    }

    async Task IniciarArrastreAsync(TerminalInstanceViewModel instancia, PointerPressedEventArgs disparador)
    {
        var item = new DataTransferItem();
        item.Set(FormatoArrastreTerminal, new PayloadArrastreTerminal(instancia));
        var datos = new DataTransfer();
        datos.Add(item);

        await DragDrop.DoDragDropAsync(disparador, datos, DragDropEffects.Move);
    }

    void OcultarInstancia_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: TerminalInstanceViewModel instancia } && ViewModel is { } vm)
            vm.CerrarDeVistaCommand.Execute(instancia);
        e.Handled = true; // no dejar que el click tambien active/arrastre la pestaña
    }

    void EliminarInstancia_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: TerminalInstanceViewModel instancia } && ViewModel is { } vm)
            vm.EliminarCommand.Execute(instancia);
        e.Handled = true;
    }

    // ─────────────────────────── soltar: reordena, nunca apila ───────────────────────────

    void Columnas_DragOver(object? sender, DragEventArgs e)
    {
        // ViewModel is not null alcanza para aceptar el drop: si no hay NINGUNA columna visible
        // todavia (recien se creo la primera terminal extra con "+"), soltar ahi debe poder
        // mostrarla igual, como la primera columna
        if (ObtenerPayload(e) == null || ViewModel is not { } vm)
        {
            e.DragEffects = DragDropEffects.None;
            _lineaInsercion.IsVisible = false;
            return;
        }

        e.DragEffects = DragDropEffects.Move;
        MostrarLineaEn(CalcularIndiceDestino(e.GetPosition(_columnas), vm.InstanciasVisibles.Count()));
    }

    void Columnas_Drop(object? sender, DragEventArgs e)
    {
        _lineaInsercion.IsVisible = false;
        if (ViewModel is not { } vm) return;
        var payload = ObtenerPayload(e);
        if (payload == null) return;

        int destino = CalcularIndiceDestino(e.GetPosition(_columnas), vm.InstanciasVisibles.Count());
        vm.MostrarEnColumnaEnPosicion(payload.Instancia, destino);
    }

    static PayloadArrastreTerminal? ObtenerPayload(DragEventArgs e) =>
        e.DataTransfer.Items.Select(i => i.TryGetValue(FormatoArrastreTerminal)).FirstOrDefault(p => p != null);

    // en que posicion de la lista quedaria si se soltara ahi: columna bajo el mouse (por X, ya que
    // UniformGrid les da a todas el mismo ancho) + si esta en su mitad izquierda o derecha
    int CalcularIndiceDestino(Point puntoEnColumnas, int cantidadColumnas)
    {
        if (cantidadColumnas == 0) return 0;
        double anchoColumna = _columnas.Bounds.Width / cantidadColumnas;
        if (anchoColumna <= 0) return 0;

        int columna = Math.Clamp((int)(puntoEnColumnas.X / anchoColumna), 0, cantidadColumnas - 1);
        double xDentroDeColumna = puntoEnColumnas.X - columna * anchoColumna;
        return xDentroDeColumna > anchoColumna / 2 ? columna + 1 : columna;
    }

    void MostrarLineaEn(int destino)
    {
        if (ViewModel is not { } vm || vm.InstanciasVisibles.Count() == 0) { _lineaInsercion.IsVisible = false; return; }

        double anchoColumna = _columnas.Bounds.Width / vm.InstanciasVisibles.Count();
        double x = Math.Clamp(destino * anchoColumna - 1.5, 0, Math.Max(0, _columnas.Bounds.Width - 3));

        _lineaInsercion.IsVisible = true;
        _lineaInsercion.Margin = new Thickness(x, 0, 0, 0);
        _lineaInsercion.Height = _columnas.Bounds.Height;
    }
}
