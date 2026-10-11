using System.Collections.Specialized;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Threading;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

record PayloadArrastreTerminal(TerminalInstanceViewModel Instancia);

public partial class TerminalView : UserControl
{
    static readonly DataFormat<PayloadArrastreTerminal> FormatoArrastreTerminal =
        DataFormat.CreateInProcessFormat<PayloadArrastreTerminal>("ggez/terminal-tab");

    const double UmbralArrastre = 5;

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

    // IsVisible atado a mano: un Style sobre ContentPresenter cascadea a todos los ContentPresenter internos
    void Columnas_ContainerPrepared(object? sender, ContainerPreparedEventArgs e) =>
        e.Container.Bind(Visual.IsVisibleProperty, new Binding(nameof(TerminalInstanceViewModel.EnColumna)));

    void SeguirErroresAlFinalSiCorresponde(object? sender, NotifyCollectionChangedEventArgs e)
    {
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

    // El click se difiere a PointerReleased: el comando saca el chip de la lista y destruiria el control antes del arrastre
    void InstanciaOculta_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TerminalInstanceViewModel instancia }) return;
        _candidatoArrastre = (instancia, e.GetPosition(this), e);
        e.Pointer.Capture((IInputElement)sender);
    }

    void InstanciaOculta_PointerMoved(object? sender, PointerEventArgs e) => Pestana_PointerMoved(sender, e);

    void InstanciaOculta_PointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        var candidato = _candidatoArrastre;
        _candidatoArrastre = null;
        e.Pointer.Capture(null);
        if (candidato is { } c && ViewModel is { } vm) vm.MostrarEnColumnaCommand.Execute(c.Instancia);
    }

    void Pestana_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: TerminalInstanceViewModel instancia } || ViewModel is not { } vm) return;
        vm.EnfocarInstanciaCommand.Execute(instancia);
        _candidatoArrastre = (instancia, e.GetPosition(this), e);
        // Captura explicita: en Wayland/XWayland PointerMoved deja de reportar bien el boton y el arrastre se cancelaba
        e.Pointer.Capture((IInputElement)sender);
    }

    void Pestana_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_candidatoArrastre is not { } candidato) return;
        double distancia = Point.Distance(candidato.Inicio, e.GetPosition(this));
        if (distancia < UmbralArrastre) return;

        _candidatoArrastre = null;
        e.Pointer.Capture(null);
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
        e.Handled = true;
    }

    void EliminarInstancia_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: TerminalInstanceViewModel instancia } && ViewModel is { } vm)
            vm.EliminarCommand.Execute(instancia);
        e.Handled = true;
    }

    void Columnas_DragOver(object? sender, DragEventArgs e)
    {
        // Se acepta el drop aunque no haya ninguna columna visible todavia
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
