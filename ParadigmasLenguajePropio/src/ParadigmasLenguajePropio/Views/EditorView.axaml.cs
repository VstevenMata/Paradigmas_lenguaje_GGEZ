using System.ComponentModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Threading;
using ParadigmasLenguajePropio.Controls;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

// carga en el DataObject/DataTransfer de un arrastre de pestaña: de que panel salio, ademas de
// la pestaña en si, para que el que la recibe sepa de donde sacarla si termina en otro panel
record PayloadArrastreTab(EditorTabViewModel Tab, EditorViewModel Origen);

public partial class EditorView : UserControl
{
    static readonly DataFormat<PayloadArrastreTab> FormatoArrastreTab =
        DataFormat.CreateInProcessFormat<PayloadArrastreTab>("ggez/editor-tab");

    const double UmbralArrastre = 5; // pixeles antes de que un click-y-mantener se considere arrastre

    EditorViewModel? ViewModel => DataContext as EditorViewModel;

    ScrollViewer _scrollVertical = null!;
    ScrollViewer _scrollHorizontal = null!;
    EditorTextArea _textArea = null!;
    EditorGutter _gutter = null!;
    EditorMinimap _minimapa = null!;
    ScrollBar _scrollBarVertical = null!;
    TextBox _campoBuscar = null!;
    TextBox _campoIrALinea = null!;
    Grid _gridRaiz = null!;
    ScrollViewer _scrollTabs = null!;
    Grid _gridContenido = null!;
    Border _overlayDrop = null!;
    EditorTabViewModel? _tabConScrollReiniciado;
    (EditorTabViewModel Tab, Point Inicio, PointerPressedEventArgs Disparador)? _candidatoArrastre;

    // lo fija EditorAreaView al construir esta vista (ver EditorAreaView.Construir): si ya se
    // llego al tope de paneles, partir mas se muestra bloqueado (el rechazo de verdad, sin
    // confiar en esto, lo hace EditorAreaViewModel.MoverTab)
    public bool PuedeDividir { get; set; } = true;

    // se dispara al soltar una pestaña (propia o de otro EditorView) sobre este panel; lo escucha
    // EditorAreaView para pedirle a EditorAreaViewModel que la mueva/parta
    public event Action<EditorTabViewModel, EditorViewModel, EditorViewModel, ZonaDrop>? TabSoltado;

    // la llama EditorAreaView tras reconstruir el arbol de paneles, para que el panel enfocado
    // reciba el foco de teclado de verdad (una vista nueva no lo tiene por si sola)
    public void EnfocarTexto() => _textArea.Focus();

    public EditorView()
    {
        InitializeComponent();

        _scrollVertical = this.FindControl<ScrollViewer>("PART_ScrollVertical")!;
        _scrollHorizontal = this.FindControl<ScrollViewer>("PART_ScrollHorizontal")!;
        _textArea = this.FindControl<EditorTextArea>("PART_TextArea")!;
        _gutter = this.FindControl<EditorGutter>("PART_Gutter")!;
        _minimapa = this.FindControl<EditorMinimap>("PART_Minimap")!;
        _scrollBarVertical = this.FindControl<ScrollBar>("PART_ScrollBarVertical")!;
        _campoBuscar = this.FindControl<TextBox>("PART_CampoBuscar")!;
        _campoIrALinea = this.FindControl<TextBox>("PART_CampoIrALinea")!;
        _gridRaiz = this.FindControl<Grid>("PART_GridRaiz")!;
        _scrollTabs = this.FindControl<ScrollViewer>("PART_ScrollTabs")!;
        _gridContenido = this.FindControl<Grid>("PART_GridContenido")!;
        _overlayDrop = this.FindControl<Border>("PART_OverlayDrop")!;

        _textArea.CursorMovido += AsegurarCursorVisible;
        _textArea.ZoomSolicitado += direccion => ViewModel?.CambiarZoom(direccion);

        // minimapa/scrollbar propios de este panel (ver comentario en el XAML) - se sincronizan
        // con el ScrollViewer del propio panel, sin depender de cual panel tiene el foco
        _scrollVertical.ScrollChanged += (_, _) => SincronizarMinimapa();
        _minimapa.DesplazamientoSolicitado += y => _scrollVertical.Offset = new Vector(_scrollVertical.Offset.X, y);
        _scrollBarVertical.Scroll += (_, e) => _scrollVertical.Offset = new Vector(_scrollVertical.Offset.X, e.NewValue);
        Loaded += (_, _) => SincronizarMinimapa();

        DragDrop.SetAllowDrop(_gridRaiz, true);
        DragDrop.AddDragOverHandler(_gridRaiz, GridRaiz_DragOver);
        DragDrop.AddDragLeaveHandler(_gridRaiz, (_, _) => OcultarOverlay());
        DragDrop.AddDropHandler(_gridRaiz, GridRaiz_Drop);

        DataContextChanged += (_, _) =>
        {
            if (ViewModel is not { } vm) return;
            vm.PropertyChanged += Vm_PropertyChanged;
        };
    }

    void Vm_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (ViewModel is not { } vm) return;

        if (e.PropertyName == nameof(EditorViewModel.TabActiva) && vm.TabActiva != _tabConScrollReiniciado)
        {
            _tabConScrollReiniciado = vm.TabActiva;
            // cada pestaña arranca mostrando su propio inicio, como CambiarArchivoActivo en el original
            Dispatcher.UIThread.Post(() =>
            {
                _scrollVertical.Offset = new Vector(0, 0);
                _scrollHorizontal.Offset = new Vector(0, 0);
                _textArea.Focus();
            });
        }
        else if (e.PropertyName == nameof(EditorViewModel.BuscarVisible) && vm.BuscarVisible)
        {
            Dispatcher.UIThread.Post(() => { _campoBuscar.Focus(); _campoBuscar.SelectAll(); });
        }
        else if (e.PropertyName == nameof(EditorViewModel.IrALineaVisible) && vm.IrALineaVisible)
        {
            Dispatcher.UIThread.Post(() => { _campoIrALinea.Focus(); _campoIrALinea.SelectAll(); });
        }
    }

    // reemplaza AsegurarCursorVisible: en vez de recalcular scroll.Valor a mano, nudgea el
    // Offset de los ScrollViewer nativos para que el rect del cursor quede dentro del viewport
    void AsegurarCursorVisible()
    {
        var rectCursor = _textArea.ObtenerRectCursor();

        var vOffset = _scrollVertical.Offset;
        var vViewport = _scrollVertical.Viewport;
        double nuevoY = vOffset.Y;
        if (rectCursor.Top < vOffset.Y) nuevoY = rectCursor.Top;
        else if (rectCursor.Bottom > vOffset.Y + vViewport.Height) nuevoY = rectCursor.Bottom - vViewport.Height;
        _scrollVertical.Offset = new Vector(vOffset.X, Math.Max(0, nuevoY));

        var hOffset = _scrollHorizontal.Offset;
        var hViewport = _scrollHorizontal.Viewport;
        double nuevoX = hOffset.X;
        if (rectCursor.Left < hOffset.X) nuevoX = rectCursor.Left;
        else if (rectCursor.Right > hOffset.X + hViewport.Width - 20) nuevoX = rectCursor.Right - hViewport.Width + 20;
        _scrollHorizontal.Offset = new Vector(Math.Max(0, nuevoX), hOffset.Y);
    }

    void SincronizarMinimapa()
    {
        _minimapa.AltoTotal = _scrollVertical.Extent.Height;
        _minimapa.AltoVisible = _scrollVertical.Viewport.Height;
        _minimapa.Desplazamiento = _scrollVertical.Offset.Y;

        // idem para el area de texto y el gutter: asi Render() sabe que rango de lineas dibujar
        // en vez de redibujar el archivo entero en cada scroll/resize (ver comentario en
        // EditorTextArea.DesplazamientoY)
        _textArea.DesplazamientoY = _scrollVertical.Offset.Y;
        _textArea.AltoVisible = _scrollVertical.Viewport.Height;
        _gutter.DesplazamientoY = _scrollVertical.Offset.Y;
        _gutter.AltoVisible = _scrollVertical.Viewport.Height;

        // mismos tres datos, ahora para el ScrollBar propio: asignar Value aca no dispara su
        // evento Scroll (ese solo sale de interaccion del usuario), asi que no hay vuelta
        // infinita con el handler que hace lo opuesto (ver constructor)
        _scrollBarVertical.Maximum = Math.Max(0, _scrollVertical.Extent.Height - _scrollVertical.Viewport.Height);
        _scrollBarVertical.ViewportSize = _scrollVertical.Viewport.Height;
        _scrollBarVertical.Value = _scrollVertical.Offset.Y;
    }

    void Tab_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Control { DataContext: EditorTabViewModel tab } || ViewModel is not { } vm) return;
        vm.CambiarArchivoActivo(tab); // el click simple activa de inmediato, arrastre o no
        _candidatoArrastre = (tab, e.GetPosition(this), e);
    }

    // recien al moverse mas de UmbralArrastre con el boton apretado se considera un arrastre real
    // (si no, cualquier click con el mas minimo temblor de mano dispararia un drag-and-drop)
    void Tab_PointerMoved(object? sender, PointerEventArgs e)
    {
        if (_candidatoArrastre is not { } candidato) return;
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed) { _candidatoArrastre = null; return; }
        if (Point.Distance(candidato.Inicio, e.GetPosition(this)) < UmbralArrastre) return;

        _candidatoArrastre = null; // una sola vez por gesto
        _ = IniciarArrastreAsync(candidato.Tab, candidato.Disparador);
    }

    void Tab_PointerReleased(object? sender, PointerReleasedEventArgs e) => _candidatoArrastre = null;

    async Task IniciarArrastreAsync(EditorTabViewModel tab, PointerPressedEventArgs disparador)
    {
        if (ViewModel is not { } vm) return;

        var item = new DataTransferItem();
        item.Set(FormatoArrastreTab, new PayloadArrastreTab(tab, vm));
        var datos = new DataTransfer();
        datos.Add(item);

        await DragDrop.DoDragDropAsync(disparador, datos, DragDropEffects.Move);
    }

    void GridRaiz_DragOver(object? sender, DragEventArgs e)
    {
        if (ObtenerPayload(e) == null) { e.DragEffects = DragDropEffects.None; OcultarOverlay(); return; }

        var zona = CalcularZona(e.GetPosition(_gridRaiz));
        bool bloqueado = zona != ZonaDrop.Centro && !PuedeDividir;
        e.DragEffects = bloqueado ? DragDropEffects.None : DragDropEffects.Move;
        MostrarOverlay(zona, bloqueado);
    }

    void GridRaiz_Drop(object? sender, DragEventArgs e)
    {
        OcultarOverlay();
        if (ViewModel is not { } vm) return;
        var payload = ObtenerPayload(e);
        if (payload == null) return;

        TabSoltado?.Invoke(payload.Tab, payload.Origen, vm, CalcularZona(e.GetPosition(_gridRaiz)));
    }

    static PayloadArrastreTab? ObtenerPayload(DragEventArgs e) =>
        e.DataTransfer.Items.Select(i => i.TryGetValue(FormatoArrastreTab)).FirstOrDefault(p => p != null);

    // dentro de la franja de tabs -> Centro (mover sin partir); dentro del area de texto: 25% de
    // arriba/abajo -> Arriba/Abajo (se chequea primero: una esquina resuelve a arriba/abajo antes
    // que a izquierda/derecha, arbitrario pero razonable), 25% de izquierda/derecha -> Izquierda/
    // Derecha, el 50% central -> Centro
    ZonaDrop CalcularZona(Point puntoEnGridRaiz)
    {
        double alturaTabs = _scrollTabs.Bounds.Height;
        if (puntoEnGridRaiz.Y < alturaTabs) return ZonaDrop.Centro;

        double anchoContenido = _gridContenido.Bounds.Width;
        double altoContenido = _gridRaiz.Bounds.Height - alturaTabs;
        if (altoContenido <= 0 || anchoContenido <= 0) return ZonaDrop.Centro;

        double fx = puntoEnGridRaiz.X / anchoContenido;
        double fy = (puntoEnGridRaiz.Y - alturaTabs) / altoContenido;

        if (fy < 0.25) return ZonaDrop.Arriba;
        if (fy > 0.75) return ZonaDrop.Abajo;
        if (fx < 0.25) return ZonaDrop.Izquierda;
        if (fx > 0.75) return ZonaDrop.Derecha;
        return ZonaDrop.Centro;
    }

    static readonly IBrush FondoPermitido = new SolidColorBrush(Color.Parse("#4055AAFF"));
    static readonly IBrush BordePermitido = new SolidColorBrush(Color.Parse("#FF3070C0"));
    static readonly IBrush FondoBloqueado = new SolidColorBrush(Color.Parse("#40D04040"));
    static readonly IBrush BordeBloqueado = new SolidColorBrush(Color.Parse("#FFC03030"));

    void MostrarOverlay(ZonaDrop zona, bool bloqueado)
    {
        _overlayDrop.IsVisible = true;
        _overlayDrop.Background = bloqueado ? FondoBloqueado : FondoPermitido;
        _overlayDrop.BorderBrush = bloqueado ? BordeBloqueado : BordePermitido;

        _overlayDrop.HorizontalAlignment = HorizontalAlignment.Stretch;
        _overlayDrop.VerticalAlignment = VerticalAlignment.Stretch;
        _overlayDrop.Width = double.NaN;
        _overlayDrop.Height = double.NaN;

        switch (zona)
        {
            case ZonaDrop.Arriba:
                _overlayDrop.VerticalAlignment = VerticalAlignment.Top;
                _overlayDrop.Height = _gridContenido.Bounds.Height * 0.25;
                break;
            case ZonaDrop.Abajo:
                _overlayDrop.VerticalAlignment = VerticalAlignment.Bottom;
                _overlayDrop.Height = _gridContenido.Bounds.Height * 0.25;
                break;
            case ZonaDrop.Izquierda:
                _overlayDrop.HorizontalAlignment = HorizontalAlignment.Left;
                _overlayDrop.Width = _gridContenido.Bounds.Width * 0.25;
                break;
            case ZonaDrop.Derecha:
                _overlayDrop.HorizontalAlignment = HorizontalAlignment.Right;
                _overlayDrop.Width = _gridContenido.Bounds.Width * 0.25;
                break;
        }
    }

    void OcultarOverlay() => _overlayDrop.IsVisible = false;

    void Cerrar_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Control { DataContext: EditorTabViewModel tab } && ViewModel is { } vm)
            _ = vm.CerrarArchivoAsync(tab);
        e.Handled = true; // no dejar que el click tambien seleccione la pestaña
    }

    void Segmento_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is Border { DataContext: BreadcrumbSegmentoViewModel segmento } && ViewModel is { } vm)
            vm.AlternarSegmentoBreadcrumb(segmento);
    }

    void ItemBreadcrumb_PointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (sender is not Border { DataContext: NodoBreadcrumb nodo } || ViewModel is not { } vm) return;
        vm.BreadcrumbSegmentos.FirstOrDefault(s => s.EsAbierto)?.Elegir(nodo);
    }

    void ItemBreadcrumb_PointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: NodoBreadcrumb nodo }) nodo.EsResaltado = true;
    }

    void ItemBreadcrumb_PointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Border { DataContext: NodoBreadcrumb nodo }) nodo.EsResaltado = false;
    }

    void CampoBuscar_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Key == Key.Enter) { vm.BuscarSiguiente(); e.Handled = true; }
        else if (e.Key == Key.Escape) { vm.BuscarVisible = false; e.Handled = true; }
    }

    void BuscarAnterior_Click(object? sender, RoutedEventArgs e) => ViewModel?.BuscarAnterior();
    void BuscarSiguiente_Click(object? sender, RoutedEventArgs e) => ViewModel?.BuscarSiguiente();
    void CerrarBusqueda_Click(object? sender, RoutedEventArgs e) { if (ViewModel is { } vm) vm.BuscarVisible = false; }
    void Reemplazar_Click(object? sender, RoutedEventArgs e) => ViewModel?.ReemplazarUno();
    void ReemplazarTodo_Click(object? sender, RoutedEventArgs e) => ViewModel?.ReemplazarTodo();

    void CampoIrALinea_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Key == Key.Enter) { vm.ConfirmarIrALinea(); e.Handled = true; }
        else if (e.Key == Key.Escape) { vm.IrALineaVisible = false; e.Handled = true; }
    }

    void CerrarIrALinea_Click(object? sender, RoutedEventArgs e) { if (ViewModel is { } vm) vm.IrALineaVisible = false; }

    // atajos de teclado esperables (Ctrl+F/Ctrl+H/Ctrl+G) que el original no tenia (solo menu/toolbar);
    // se agregan aca a nivel del EditorView entero porque el foco normalmente lo tiene EditorTextArea
    void Editor_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (!e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;

        if (e.Key == Key.F) { vm.BuscarVisible = true; vm.MostrarReemplazo = false; e.Handled = true; }
        else if (e.Key == Key.H) { vm.BuscarVisible = true; vm.MostrarReemplazo = true; e.Handled = true; }
        else if (e.Key == Key.G) { vm.IrALineaVisible = true; e.Handled = true; }
    }
}
