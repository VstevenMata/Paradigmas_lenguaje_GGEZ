using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

public enum TipoEdicion { Ninguna, Escritura, Borrado, Otro }

// Reemplaza ArchivoAbierto (clase anidada en PanelCentral.cs) + buena parte de la lógica de edición
// de PanelCentral.cs: modelo de líneas/cursor/selección/undo portado casi verbatim (no dependía de
// Raylib), solo el render/hit-testing/scroll se rehacen en EditorTextArea/EditorGutter.
public partial class EditorTabViewModel : ViewModelBase
{
    const int LimiteHistorial = 200;

    [ObservableProperty]
    private string _nombre = "";

    [ObservableProperty]
    private string _rutaCompleta = "";

    [ObservableProperty]
    private bool _modificado;

    [ObservableProperty]
    private bool _esActiva;

    // lista mutable en el lugar (no ObservableCollection): notificar por cada letra tipeada sería
    // carísimo. La vista se entera de los cambios via ContenidoCambiado, no por bindings de Lineas.
    public List<string> Lineas = new() { "" };

    public int FilaCursor, ColumnaCursor, ColumnaDeseada;
    public bool HaySeleccion;
    public int SelFila, SelColumna;

    // se disparan a mano al final de cada método que muta estado, en vez de INotifyPropertyChanged
    // por campo (que sería carísimo para Lineas en cada tecla)
    public event Action? ContenidoCambiado; // cambio de líneas: hace falta remedir/rewrap
    public event Action? CursorCambiado; // solo caret/selección: alcanza con repintar

    readonly List<(List<string> Lineas, int Fila, int Columna)> _historialDeshacer = new();
    readonly List<(List<string> Lineas, int Fila, int Columna)> _historialRehacer = new();
    TipoEdicion _ultimoTipoEdicion = TipoEdicion.Ninguna;

    public string Texto => string.Join('\n', Lineas);

    public EditorTabViewModel(string nombre, string rutaCompleta, List<string>? lineas = null)
    {
        Nombre = nombre;
        RutaCompleta = rutaCompleta;
        if (lineas != null) Lineas = lineas;
    }

    void AvisarContenido() => ContenidoCambiado?.Invoke();
    void AvisarCursor() => CursorCambiado?.Invoke();

    // ─────────────────────────── edición ───────────────────────────

    public void InsertarTexto(string texto)
    {
        bool esUnCaracter = texto.Length == 1;
        GuardarEstadoParaDeshacer(esUnCaracter ? TipoEdicion.Escritura : TipoEdicion.Otro);

        if (HaySeleccion) BorrarSeleccionInterno();

        var partes = texto.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        string antes = Lineas[FilaCursor][..ColumnaCursor];
        string despues = Lineas[FilaCursor][ColumnaCursor..];

        if (partes.Length == 1)
        {
            Lineas[FilaCursor] = antes + partes[0] + despues;
            ColumnaCursor += partes[0].Length;
        }
        else
        {
            Lineas[FilaCursor] = antes + partes[0];
            for (int i = 1; i < partes.Length - 1; i++)
                Lineas.Insert(FilaCursor + i, partes[i]);
            Lineas.Insert(FilaCursor + partes.Length - 1, partes[^1] + despues);
            FilaCursor += partes.Length - 1;
            ColumnaCursor = partes[^1].Length;
        }
        ColumnaDeseada = ColumnaCursor;
        AvisarContenido();
    }

    public void InsertarNuevaLinea()
    {
        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        if (HaySeleccion) BorrarSeleccionInterno();

        string antes = Lineas[FilaCursor][..ColumnaCursor];
        string despues = Lineas[FilaCursor][ColumnaCursor..];
        Lineas[FilaCursor] = antes;
        Lineas.Insert(FilaCursor + 1, despues);
        FilaCursor++;
        ColumnaCursor = 0;
        ColumnaDeseada = 0;
        AvisarContenido();
    }

    public void Retroceder()
    {
        if (HaySeleccion)
        {
            GuardarEstadoParaDeshacer(TipoEdicion.Otro);
            BorrarSeleccionInterno();
            AvisarContenido();
            return;
        }

        GuardarEstadoParaDeshacer(TipoEdicion.Borrado);
        if (ColumnaCursor > 0)
        {
            Lineas[FilaCursor] = Lineas[FilaCursor].Remove(ColumnaCursor - 1, 1);
            ColumnaCursor--;
        }
        else if (FilaCursor > 0)
        {
            int columnaUnion = Lineas[FilaCursor - 1].Length;
            Lineas[FilaCursor - 1] += Lineas[FilaCursor];
            Lineas.RemoveAt(FilaCursor);
            FilaCursor--;
            ColumnaCursor = columnaUnion;
        }
        ColumnaDeseada = ColumnaCursor;
        AvisarContenido();
    }

    public void Suprimir()
    {
        if (HaySeleccion)
        {
            GuardarEstadoParaDeshacer(TipoEdicion.Otro);
            BorrarSeleccionInterno();
            AvisarContenido();
            return;
        }

        GuardarEstadoParaDeshacer(TipoEdicion.Borrado);
        if (ColumnaCursor < Lineas[FilaCursor].Length)
        {
            Lineas[FilaCursor] = Lineas[FilaCursor].Remove(ColumnaCursor, 1);
        }
        else if (FilaCursor < Lineas.Count - 1)
        {
            Lineas[FilaCursor] += Lineas[FilaCursor + 1];
            Lineas.RemoveAt(FilaCursor + 1);
        }
        AvisarContenido();
    }

    public void MoverLineaArriba()
    {
        if (FilaCursor == 0) return;
        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        (Lineas[FilaCursor - 1], Lineas[FilaCursor]) = (Lineas[FilaCursor], Lineas[FilaCursor - 1]);
        FilaCursor--;
        AvisarContenido();
    }

    public void MoverLineaAbajo()
    {
        if (FilaCursor == Lineas.Count - 1) return;
        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        (Lineas[FilaCursor + 1], Lineas[FilaCursor]) = (Lineas[FilaCursor], Lineas[FilaCursor + 1]);
        FilaCursor++;
        AvisarContenido();
    }

    public void SeleccionarTodo()
    {
        SelFila = 0;
        SelColumna = 0;
        HaySeleccion = true;
        FilaCursor = Lineas.Count - 1;
        ColumnaCursor = Lineas[FilaCursor].Length;
        ColumnaDeseada = ColumnaCursor;
        AvisarCursor();
    }

    public string TextoSeleccionado()
    {
        if (!HaySeleccion) return "";
        var (fi, ci) = SeleccionDesde();
        var (ff, cf) = SeleccionHasta();
        if (fi == ff) return Lineas[fi][ci..cf];

        var sb = new StringBuilder();
        sb.Append(Lineas[fi][ci..]);
        for (int f = fi + 1; f < ff; f++)
        {
            sb.Append('\n');
            sb.Append(Lineas[f]);
        }
        sb.Append('\n');
        sb.Append(Lineas[ff][..cf]);
        return sb.ToString();
    }

    public void BorrarSeleccion()
    {
        if (!HaySeleccion) return;
        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        BorrarSeleccionInterno();
        AvisarContenido();
    }

    void BorrarSeleccionInterno()
    {
        if (!HaySeleccion) return;
        var (fi, ci) = SeleccionDesde();
        var (ff, cf) = SeleccionHasta();

        if (fi == ff)
        {
            Lineas[fi] = Lineas[fi].Remove(ci, cf - ci);
        }
        else
        {
            Lineas[fi] = Lineas[fi][..ci] + Lineas[ff][cf..];
            Lineas.RemoveRange(fi + 1, ff - fi);
        }

        FilaCursor = fi;
        ColumnaCursor = ci;
        ColumnaDeseada = ci;
        HaySeleccion = false;
    }

    public bool AnclaEsAntesDelCursor() => SelFila != FilaCursor ? SelFila < FilaCursor : SelColumna < ColumnaCursor;
    public (int fila, int columna) SeleccionDesde() => AnclaEsAntesDelCursor() ? (SelFila, SelColumna) : (FilaCursor, ColumnaCursor);
    public (int fila, int columna) SeleccionHasta() => AnclaEsAntesDelCursor() ? (FilaCursor, ColumnaCursor) : (SelFila, SelColumna);

    // ─────────────────────────── cursor ───────────────────────────

    public void MoverCursor(int fila, int columna, bool extenderSeleccion)
    {
        fila = Math.Clamp(fila, 0, Lineas.Count - 1);
        columna = Math.Clamp(columna, 0, Lineas[fila].Length);

        if (extenderSeleccion)
        {
            if (!HaySeleccion)
            {
                HaySeleccion = true;
                SelFila = FilaCursor;
                SelColumna = ColumnaCursor;
            }
        }
        else
        {
            HaySeleccion = false;
        }

        FilaCursor = fila;
        ColumnaCursor = columna;
        AvisarCursor();
    }

    public void MoverIzquierda(bool extender)
    {
        if (ColumnaCursor > 0) MoverCursor(FilaCursor, ColumnaCursor - 1, extender);
        else if (FilaCursor > 0) MoverCursor(FilaCursor - 1, Lineas[FilaCursor - 1].Length, extender);
        ColumnaDeseada = ColumnaCursor;
    }

    public void MoverDerecha(bool extender)
    {
        if (ColumnaCursor < Lineas[FilaCursor].Length) MoverCursor(FilaCursor, ColumnaCursor + 1, extender);
        else if (FilaCursor < Lineas.Count - 1) MoverCursor(FilaCursor + 1, 0, extender);
        ColumnaDeseada = ColumnaCursor;
    }

    public void MoverArriba(bool extender)
    {
        if (FilaCursor == 0) { MoverCursor(0, 0, extender); ColumnaDeseada = 0; return; }
        MoverCursor(FilaCursor - 1, Math.Min(ColumnaDeseada, Lineas[FilaCursor - 1].Length), extender);
    }

    public void MoverAbajo(bool extender)
    {
        if (FilaCursor == Lineas.Count - 1) { MoverCursor(FilaCursor, Lineas[FilaCursor].Length, extender); ColumnaDeseada = ColumnaCursor; return; }
        MoverCursor(FilaCursor + 1, Math.Min(ColumnaDeseada, Lineas[FilaCursor + 1].Length), extender);
    }

    public void MoverInicioLinea(bool extender)
    {
        MoverCursor(FilaCursor, 0, extender);
        ColumnaDeseada = 0;
    }

    public void MoverFinLinea(bool extender)
    {
        MoverCursor(FilaCursor, Lineas[FilaCursor].Length, extender);
        ColumnaDeseada = ColumnaCursor;
    }

    // deja el cursor al inicio de esa línea (1-based, como se le muestra al usuario)
    public void IrALinea(int numeroLinea)
    {
        FilaCursor = Math.Clamp(numeroLinea - 1, 0, Lineas.Count - 1);
        ColumnaCursor = 0;
        ColumnaDeseada = 0;
        HaySeleccion = false;
        AvisarCursor();
    }

    // ─────────────────────────── buscar / reemplazar ───────────────────────────

    public (int actual, int total) Buscar(string patron, bool distingueMayus, bool siguiente)
    {
        if (string.IsNullOrEmpty(patron))
        {
            HaySeleccion = false;
            return (0, 0);
        }

        var coincidencias = EncontrarCoincidencias(Texto, patron, distingueMayus);
        if (coincidencias.Count == 0)
        {
            HaySeleccion = false;
            return (0, 0);
        }

        int offsetCursor = OffsetDesdePosicion(FilaCursor, ColumnaCursor);
        int offsetAncla = HaySeleccion ? OffsetDesdePosicion(SelFila, SelColumna) : offsetCursor;
        int offsetInicio = Math.Min(offsetAncla, offsetCursor);
        int offsetFin = Math.Max(offsetAncla, offsetCursor);

        int indiceElegido = siguiente
            ? coincidencias.FindIndex(o => o >= offsetFin)
            : coincidencias.FindLastIndex(o => o < offsetInicio);
        if (indiceElegido < 0) indiceElegido = siguiente ? 0 : coincidencias.Count - 1; // da la vuelta

        int offsetMatch = coincidencias[indiceElegido];
        var inicio = PosicionDesdeOffset(offsetMatch);
        var fin = PosicionDesdeOffset(offsetMatch + patron.Length);

        SelFila = inicio.fila;
        SelColumna = inicio.columna;
        FilaCursor = fin.fila;
        ColumnaCursor = fin.columna;
        ColumnaDeseada = ColumnaCursor;
        HaySeleccion = true;

        AvisarCursor();
        return (indiceElegido + 1, coincidencias.Count);
    }

    public (int actual, int total) Reemplazar(string patron, string reemplazo, bool distingueMayus)
    {
        if (string.IsNullOrEmpty(patron)) return (0, 0);
        if (HaySeleccion && CoincideSeleccionConPatron(patron, distingueMayus))
            InsertarTexto(reemplazo);
        return Buscar(patron, distingueMayus, siguiente: true);
    }

    public int ReemplazarTodo(string patron, string reemplazo, bool distingueMayus)
    {
        if (string.IsNullOrEmpty(patron)) return 0;

        var comparacion = distingueMayus ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        string texto = Texto;
        var sb = new StringBuilder();
        int idx = 0;
        int total = 0;
        while (true)
        {
            int encontrado = texto.IndexOf(patron, idx, comparacion);
            if (encontrado < 0)
            {
                sb.Append(texto, idx, texto.Length - idx);
                break;
            }
            sb.Append(texto, idx, encontrado - idx);
            sb.Append(reemplazo);
            idx = encontrado + patron.Length;
            total++;
        }
        if (total == 0) return 0;

        GuardarEstadoParaDeshacer(TipoEdicion.Otro);
        Lineas.Clear();
        Lineas.AddRange(sb.ToString().Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'));
        FilaCursor = Math.Clamp(FilaCursor, 0, Lineas.Count - 1);
        ColumnaCursor = Math.Clamp(ColumnaCursor, 0, Lineas[FilaCursor].Length);
        HaySeleccion = false;
        AvisarContenido();
        return total;
    }

    bool CoincideSeleccionConPatron(string patron, bool distingueMayus)
    {
        var comparacion = distingueMayus ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        return string.Equals(TextoSeleccionado(), patron, comparacion);
    }

    static List<int> EncontrarCoincidencias(string texto, string patron, bool distingueMayus)
    {
        var resultado = new List<int>();
        var comparacion = distingueMayus ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        int idx = 0;
        while (true)
        {
            int encontrado = texto.IndexOf(patron, idx, comparacion);
            if (encontrado < 0) break;
            resultado.Add(encontrado);
            idx = encontrado + patron.Length;
        }
        return resultado;
    }

    (int fila, int columna) PosicionDesdeOffset(int offset)
    {
        int restante = offset;
        for (int f = 0; f < Lineas.Count; f++)
        {
            if (restante <= Lineas[f].Length) return (f, restante);
            restante -= Lineas[f].Length + 1;
        }
        return (Lineas.Count - 1, Lineas[^1].Length);
    }

    int OffsetDesdePosicion(int fila, int columna)
    {
        int offset = 0;
        for (int f = 0; f < fila; f++)
            offset += Lineas[f].Length + 1;
        return offset + columna;
    }

    // ─────────────────────────── deshacer / rehacer ───────────────────────────

    void GuardarEstadoParaDeshacer(TipoEdicion tipo)
    {
        Modificado = true;

        // escritura y borrado se agrupan mientras se repiten, así deshacer no va letra por letra
        bool seAgrupaConLaAnterior = (tipo == TipoEdicion.Escritura || tipo == TipoEdicion.Borrado) && tipo == _ultimoTipoEdicion;
        if (seAgrupaConLaAnterior) return;

        _historialDeshacer.Add((new List<string>(Lineas), FilaCursor, ColumnaCursor));
        if (_historialDeshacer.Count > LimiteHistorial) _historialDeshacer.RemoveAt(0);
        _historialRehacer.Clear();
        _ultimoTipoEdicion = tipo;
    }

    public void Deshacer()
    {
        if (_historialDeshacer.Count == 0) return;

        _historialRehacer.Add((new List<string>(Lineas), FilaCursor, ColumnaCursor));

        var (lineasPrevias, fila, columna) = _historialDeshacer[^1];
        _historialDeshacer.RemoveAt(_historialDeshacer.Count - 1);

        Lineas.Clear();
        Lineas.AddRange(lineasPrevias);
        FilaCursor = Math.Clamp(fila, 0, Lineas.Count - 1);
        ColumnaCursor = Math.Clamp(columna, 0, Lineas[FilaCursor].Length);
        ColumnaDeseada = ColumnaCursor;
        HaySeleccion = false;
        _ultimoTipoEdicion = TipoEdicion.Ninguna;
        AvisarContenido();
    }

    public void Rehacer()
    {
        if (_historialRehacer.Count == 0) return;

        _historialDeshacer.Add((new List<string>(Lineas), FilaCursor, ColumnaCursor));

        var (lineasSiguientes, fila, columna) = _historialRehacer[^1];
        _historialRehacer.RemoveAt(_historialRehacer.Count - 1);

        Lineas.Clear();
        Lineas.AddRange(lineasSiguientes);
        FilaCursor = Math.Clamp(fila, 0, Lineas.Count - 1);
        ColumnaCursor = Math.Clamp(columna, 0, Lineas[FilaCursor].Length);
        ColumnaDeseada = ColumnaCursor;
        HaySeleccion = false;
        _ultimoTipoEdicion = TipoEdicion.Ninguna;
        AvisarContenido();
    }
}
