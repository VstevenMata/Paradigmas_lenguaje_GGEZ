using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Una terminal individual: proceso, lineas, historial, autocompletado. Es lo que antes vivia
// mezclado en TerminalViewModel junto con la pestaña "Errores" y el swap con la extension de
// Claude - eso ahora vive en TerminalViewModel (el panel), que puede tener varias instancias de
// esta clase a la vez (hasta TerminalViewModel.MaxTerminales), una por proceso independiente.
public partial class TerminalInstanceViewModel : ViewModelBase
{
    const int LimiteLineas = 2000;
    const int LimiteHistorial = 100;
    const int LineasPorTick = 400; // tope de salida procesada por tick: un comando ruidoso no debe trabar la UI

    [ObservableProperty]
    private string _nombre = "Terminal";

    // si esta visible como columna en el panel (ver TerminalViewModel.InstanciasVisibles) - cerrar
    // de la vista solo apaga esto, no mata el proceso ni la saca de Instancias; eso lo hace Eliminar
    [ObservableProperty]
    private bool _enColumna = true;

    // solo una instancia es la activa a la vez (ver TerminalViewModel.InstanciaActiva) - todas las
    // columnas se ven al mismo tiempo, pero el teclado y el resaltado de foco van a esta
    [ObservableProperty]
    private bool _esActiva;

    [ObservableProperty]
    private string _entradaActual = "";

    [ObservableProperty]
    private bool _tieneFoco;

    public ObservableCollection<TerminalLinea> LineasTerminal { get; } = new();

    // el comando "exit" cierra el panel entero (igual que el toggle del menu Ver), no solo esta
    // instancia - lo conecta TerminalViewModel al crearla
    public Action? OnCerrar;

    // la salida de error de esta instancia tambien se copia a la pestaña "Errores" compartida del
    // panel (ver TerminalViewModel.AgregarError) - conectado al crearla, igual que OnCerrar
    public Action<string>? OnError;

    // clickear el contenido de esta terminal (no solo su pestaña, ver TerminalView.axaml) le pide
    // al panel que la marque como la activa - conectado al crearla, igual que OnCerrar/OnError
    public Action? OnActivar;

    readonly TerminalProcessService _proceso = new();
    readonly List<string> _historial = new();
    readonly DispatcherTimer _timer;

    int _indiceHistorial;
    string _borrador = "";
    string _directorioActual;
    string _directorioAnterior;

    public string DirectorioActual => _directorioActual;

    // fija de por vida de la instancia (no reactiva a instalar/desinstalar la extension despues):
    // si es real, esta columna corre una PTY de verdad (ver ClaudeTerminalView) en vez de la
    // simulada por pipes de mas abajo - decidido una sola vez al crearla, en TerminalViewModel
    public bool EsReal { get; }

    public TerminalInstanceViewModel(string directorioInicial, bool esReal)
    {
        EsReal = esReal;
        _directorioActual = directorioInicial;
        _directorioAnterior = directorioInicial;
        Escribir("Terminal GGEZ. Escriba \"help\" para ver los comandos propios y los atajos.", TerminalColorLinea.Aviso);

        _timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(33) };
        _timer.Tick += (_, _) => { DrenarSalida(); RevisarProcesoTerminado(); };
        _timer.Start();
    }

    void DrenarSalida()
    {
        for (int i = 0; i < LineasPorTick && _proceso.TryDequeue(out var salida); i++)
        {
            string texto = TerminalProcessService.Limpiar(salida.Texto);
            Escribir(texto, salida.EsError ? TerminalColorLinea.Error : TerminalColorLinea.Escrito);
            if (salida.EsError) OnError?.Invoke(texto);
        }
    }

    void RevisarProcesoTerminado()
    {
        if (!_proceso.RevisarProcesoTerminado(out int codigo, out bool cortado)) return;
        OnPropertyChanged(nameof(HayProcesoActivo));
        OnPropertyChanged(nameof(PrefijoEntrada));

        if (codigo != 0 && !cortado)
        {
            string aviso = $"[el comando termino con codigo {codigo}]";
            Escribir(aviso, TerminalColorLinea.Error);
            OnError?.Invoke(aviso);
        }
    }

    public bool HayProcesoActivo => _proceso.ProcesoActivo;

    // ─────────────────────────── ejecucion de comandos ───────────────────────────

    public void ConfirmarLinea()
    {
        string linea = EntradaActual;
        EntradaActual = "";

        if (_proceso.ProcesoActivo)
        {
            Escribir(linea, TerminalColorLinea.Escrito);
            var error = _proceso.EnviarEntrada(linea);
            if (error != null) Escribir(error, TerminalColorLinea.Error);
            return;
        }

        Escribir(linea, TerminalColorLinea.Escrito, PromptActual());

        if (linea.Trim().Length == 0) return;

        _historial.Remove(linea);
        _historial.Add(linea);
        if (_historial.Count > LimiteHistorial) _historial.RemoveAt(0);
        _indiceHistorial = _historial.Count;
        _borrador = "";

        if (!EjecutarComandoPropio(linea.Trim())) Lanzar(linea);
    }

    bool EjecutarComandoPropio(string linea)
    {
        int corte = linea.IndexOf(' ');
        string comando = (corte < 0 ? linea : linea[..corte]).ToLowerInvariant();
        string argumento = corte < 0 ? "" : linea[(corte + 1)..].Trim();

        switch (comando)
        {
            case "cd": CambiarDirectorio(argumento); return true;
            case "clear": case "cls": LimpiarPantalla(); return true;
            case "exit": OnCerrar?.Invoke(); return true;
            case "help": MostrarAyuda(); return true;
            default: return false;
        }
    }

    void CambiarDirectorio(string argumento)
    {
        string destino = argumento == "-" ? _directorioAnterior : ResolverRuta(argumento.Length == 0 ? "~" : argumento);

        if (!Directory.Exists(destino))
        {
            Escribir($"No existe la carpeta: {destino}", TerminalColorLinea.Error);
            return;
        }

        _directorioAnterior = _directorioActual;
        _directorioActual = Path.GetFullPath(destino);
        OnPropertyChanged(nameof(PrefijoEntrada));
    }

    void MostrarAyuda()
    {
        Escribir("Comandos propios de la terminal:", TerminalColorLinea.Aviso);
        Escribir("  cd RUTA          cambia de carpeta (acepta ~, .. y rutas relativas)", TerminalColorLinea.Escrito);
        Escribir("  cd -             vuelve a la carpeta anterior", TerminalColorLinea.Escrito);
        Escribir("  clear, cls       limpia la pantalla", TerminalColorLinea.Escrito);
        Escribir("  exit             cierra el panel de la terminal", TerminalColorLinea.Escrito);
        Escribir("  help             muestra esta ayuda", TerminalColorLinea.Escrito);
        Escribir("Cualquier otro comando lo ejecuta el shell del sistema.", TerminalColorLinea.Escrito);
        Escribir("Atajos:", TerminalColorLinea.Aviso);
        Escribir("  Flechas izq/der   mueven el cursor; con Ctrl saltan de palabra", TerminalColorLinea.Escrito);
        Escribir("  Flechas arr/abj   recorren el historial de comandos", TerminalColorLinea.Escrito);
        Escribir("  Tab               autocompleta archivos y carpetas", TerminalColorLinea.Escrito);
        Escribir("  Inicio / Fin      principio y final de la linea", TerminalColorLinea.Escrito);
        Escribir("  Ctrl+C            cancela la linea, o corta el proceso en curso", TerminalColorLinea.Escrito);
        Escribir("  Ctrl+D o Ctrl+Z   marca el fin de la entrada del proceso en curso", TerminalColorLinea.Escrito);
        Escribir("  Ctrl+L            limpia la pantalla", TerminalColorLinea.Escrito);
        Escribir("  Ctrl+U / Ctrl+W   borra la linea entera / la palabra anterior", TerminalColorLinea.Escrito);
        Escribir("  Ctrl+V            pega del portapapeles", TerminalColorLinea.Escrito);
        Escribir("  Ctrl+Shift+C      copia toda la salida", TerminalColorLinea.Escrito);
        Escribir("  RePag / AvPag     desplazan la salida", TerminalColorLinea.Escrito);
        Escribir("  Ctrl+Inicio/Fin   van al principio y al final de la salida", TerminalColorLinea.Escrito);
    }

    void Lanzar(string comando)
    {
        var error = _proceso.Lanzar(comando, _directorioActual);
        if (error != null) Escribir(error, TerminalColorLinea.Error);
        OnPropertyChanged(nameof(HayProcesoActivo));
        OnPropertyChanged(nameof(PrefijoEntrada));
    }

    public void CerrarEntradaEstandar()
    {
        if (!_proceso.ProcesoActivo) return;
        var error = _proceso.CerrarEntradaEstandar();
        if (error != null) Escribir(error, TerminalColorLinea.Error);
        Escribir("^D", TerminalColorLinea.Aviso);
    }

    public void Interrumpir()
    {
        if (_proceso.ProcesoActivo)
        {
            var error = _proceso.Interrumpir();
            if (error != null) Escribir(error, TerminalColorLinea.Error);
            Escribir("^C", TerminalColorLinea.Aviso);
            return;
        }

        Escribir(EntradaActual + "^C", TerminalColorLinea.Escrito, PromptActual());
        EntradaActual = "";
        _indiceHistorial = _historial.Count;
    }

    public void LimpiarPantalla() => LineasTerminal.Clear();

    public string TextoCompleto()
    {
        var texto = new StringBuilder();
        foreach (var linea in LineasTerminal) texto.AppendLine(linea.Prefijo + linea.Texto);
        return texto.ToString();
    }

    // ─────────────────────────── historial ───────────────────────────

    public void RecorrerHistorial(int direccion)
    {
        if (_historial.Count == 0) return;

        if (_indiceHistorial == _historial.Count && direccion < 0) _borrador = EntradaActual;

        int nuevo = Math.Clamp(_indiceHistorial + direccion, 0, _historial.Count);
        if (nuevo == _indiceHistorial) return;

        _indiceHistorial = nuevo;
        EntradaActual = _indiceHistorial == _historial.Count ? _borrador : _historial[_indiceHistorial];
    }

    // ─────────────────────────── edicion de la linea viva ───────────────────────────

    public (string Texto, int Caret) BorrarHastaElInicio(string entrada, int caret)
    {
        _indiceHistorial = _historial.Count;
        return (entrada[caret..], 0);
    }

    public (string Texto, int Caret) BorrarPalabraAtras(string entrada, int caret)
    {
        int inicio = InicioPalabraAnterior(entrada, caret);
        if (inicio == caret) return (entrada, caret);
        return (entrada.Remove(inicio, caret - inicio), inicio);
    }

    public static int InicioPalabraAnterior(string entrada, int caret)
    {
        int i = caret;
        while (i > 0 && entrada[i - 1] == ' ') i--;
        while (i > 0 && entrada[i - 1] != ' ') i--;
        return i;
    }

    public static int InicioPalabraSiguiente(string entrada, int caret)
    {
        int i = caret;
        while (i < entrada.Length && entrada[i] != ' ') i++;
        while (i < entrada.Length && entrada[i] == ' ') i++;
        return i;
    }

    public static string AplanarParaPegar(string texto) =>
        texto.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');

    // ─────────────────────────── autocompletado ───────────────────────────

    public (string Texto, int Caret) Autocompletar(string entrada, int caret)
    {
        int inicioToken = caret == 0 ? 0 : entrada.LastIndexOf(' ', caret - 1) + 1;
        string token = entrada[inicioToken..caret];

        int corte = token.LastIndexOfAny(new[] { '/', '\\' });
        string prefijoRuta = corte < 0 ? "" : token[..(corte + 1)];
        string parcial = corte < 0 ? token : token[(corte + 1)..];

        string carpeta = ResolverRuta(prefijoRuta.Length == 0 ? "." : prefijoRuta);
        if (!Directory.Exists(carpeta)) return (entrada, caret);

        List<string> candidatos;
        try
        {
            candidatos = Directory.EnumerateFileSystemEntries(carpeta)
                .Select(ruta => Path.GetFileName(ruta) ?? "")
                .Where(nombre => nombre.Length > 0 && nombre.StartsWith(parcial, StringComparison.OrdinalIgnoreCase))
                .OrderBy(nombre => nombre, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
        catch (Exception ex)
        {
            Escribir($"No se pudo leer la carpeta: {ex.Message}", TerminalColorLinea.Error);
            return (entrada, caret);
        }

        if (candidatos.Count == 0) return (entrada, caret);

        string comun = PrefijoComun(candidatos);

        if (candidatos.Count > 1 && comun.Length == parcial.Length)
        {
            Escribir(entrada, TerminalColorLinea.Escrito, PromptActual());
            foreach (var nombre in candidatos) Escribir("  " + nombre, TerminalColorLinea.Aviso);
            return (entrada, caret);
        }

        string completado = comun;
        if (candidatos.Count == 1)
            completado += Directory.Exists(Path.Combine(carpeta, candidatos[0])) ? Path.DirectorySeparatorChar.ToString() : " ";

        string nuevoToken = prefijoRuta + completado;
        string nuevaEntrada = entrada[..inicioToken] + nuevoToken + entrada[caret..];
        return (nuevaEntrada, inicioToken + nuevoToken.Length);
    }

    static string PrefijoComun(List<string> nombres)
    {
        string comun = nombres[0];
        foreach (var nombre in nombres)
        {
            int i = 0;
            while (i < comun.Length && i < nombre.Length && char.ToLowerInvariant(comun[i]) == char.ToLowerInvariant(nombre[i])) i++;
            comun = comun[..i];
        }
        return comun;
    }

    // ─────────────────────────── contenido ───────────────────────────

    void Escribir(string texto, TerminalColorLinea color, string prefijo = "")
    {
        foreach (var trozo in texto.Split('\n'))
        {
            LineasTerminal.Add(new TerminalLinea(prefijo, trozo, color));
            prefijo = "";
        }

        while (LineasTerminal.Count > LimiteLineas) LineasTerminal.RemoveAt(0);
    }

    // ─────────────────────────── prompt y rutas ───────────────────────────

    public void EstablecerDirectorio(string ruta)
    {
        if (!Directory.Exists(ruta)) return;
        _directorioAnterior = _directorioActual;
        _directorioActual = Path.GetFullPath(ruta);
        OnPropertyChanged(nameof(PrefijoEntrada));
    }

    public string PrefijoEntrada => _proceso.ProcesoActivo ? "" : PromptActual();

    public string PromptActual() => $"{Environment.UserName}@{Environment.MachineName.ToLowerInvariant()}:{RutaCorta(_directorioActual)}$ ";

    static string RutaCorta(string ruta)
    {
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return !string.IsNullOrEmpty(home) && ruta.StartsWith(home) ? "~" + ruta[home.Length..] : ruta;
    }

    string ResolverRuta(string ruta)
    {
        ruta = ruta.Trim().Trim('"');
        string home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

        if (ruta.Length == 0) return _directorioActual;
        if (ruta == "~") return home;
        if (ruta.StartsWith("~/") || ruta.StartsWith("~\\")) ruta = Path.Combine(home, ruta[2..]);

        try { return Path.GetFullPath(ruta, _directorioActual); }
        catch { return ruta; }
    }

    // corta el proceso que haya quedado vivo, tanto al eliminar esta instancia como al cerrar la app
    public void Cerrar() => _proceso.Cerrar();
}
