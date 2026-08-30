using System.Collections.ObjectModel;
using System.Text;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Reemplaza PanelTerminal.cs: la logica de comandos/historial/autocompletado se porta casi textual
// (no dependia de Raylib salvo para leer teclado, que ahora maneja TerminalView en code-behind).
// El word-wrap/scroll/cursor a mano del original los reemplaza TextBlock con TextWrapping + un
// TextBox real para la linea viva (ver TerminalView.axaml) - no hace falta CalcularCortes/AnchoChar.
public partial class TerminalViewModel : ViewModelBase
{
    const int LimiteLineas = 2000;
    const int LimiteHistorial = 100;
    const int LineasPorTick = 400; // tope de salida procesada por tick: un comando ruidoso no debe trabar la UI

    public string[] Tabs { get; } = { "Terminal", "Errores" };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(EsTabTerminal), nameof(EsTabErrores))]
    private int _tabActiva;

    public bool EsTabTerminal => TabActiva == 0;
    public bool EsTabErrores => TabActiva == 1;

    [ObservableProperty]
    private string _entradaActual = "";

    [ObservableProperty]
    private bool _tieneFoco;

    public ObservableCollection<TerminalLinea> LineasTerminal { get; } = new();
    public ObservableCollection<TerminalLinea> LineasErrores { get; } = new();

    public Action? OnCerrar; // lo dispara el comando "exit"

    readonly TerminalProcessService _proceso = new();
    readonly List<string> _historial = new();
    readonly DispatcherTimer _timer;

    int _indiceHistorial;
    string _borrador = "";
    bool _huboErroresReales;
    string _directorioActual;
    string _directorioAnterior;

    public TerminalViewModel()
    {
        _directorioActual = Environment.CurrentDirectory;
        _directorioAnterior = _directorioActual;
        LineasErrores.Add(new TerminalLinea("", "Los errores no existen", TerminalColorLinea.Escrito));
        Escribir("Terminal GGEZ. Escriba \"help\" para ver los comandos propios y los atajos.", TerminalColorLinea.Aviso);

        // reemplaza el Update() por-frame del original: drena la cola de salida y revisa si el
        // proceso termino, a un ritmo fijo en vez de atado al render loop
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
            if (salida.EsError) AgregarError(texto);
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
            AgregarError(aviso);
        }
    }

    public bool HayProcesoActivo => _proceso.ProcesoActivo;

    // ─────────────────────────── ejecucion de comandos ───────────────────────────

    public void ConfirmarLinea()
    {
        string linea = EntradaActual;
        EntradaActual = "";

        // con un proceso vivo la linea no es un comando: es lo que ese proceso espera por stdin
        if (_proceso.ProcesoActivo)
        {
            Escribir(linea, TerminalColorLinea.Escrito);
            var error = _proceso.EnviarEntrada(linea);
            if (error != null) Escribir(error, TerminalColorLinea.Error);
            return;
        }

        Escribir(linea, TerminalColorLinea.Escrito, PromptActual());

        if (linea.Trim().Length == 0) return;

        // si el comando ya estaba en el historial se mueve al final, no se duplica
        _historial.Remove(linea);
        _historial.Add(linea);
        if (_historial.Count > LimiteHistorial) _historial.RemoveAt(0);
        _indiceHistorial = _historial.Count;
        _borrador = "";

        if (!EjecutarComandoPropio(linea.Trim())) Lanzar(linea);
    }

    // devuelve true si el comando lo resuelve la propia terminal y no hace falta un proceso
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
        // "cd -" vuelve a la carpeta anterior, como en bash
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

    // sin esto, un programa que lee hasta el fin de la entrada (cat, findstr, sort) no terminaria nunca
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

    public void LimpiarPantalla()
    {
        LineasTerminal.Clear();
    }

    public string TextoCompleto()
    {
        var texto = new StringBuilder();
        foreach (var linea in TabActiva == 0 ? LineasTerminal : LineasErrores)
            texto.AppendLine(linea.Prefijo + linea.Texto);
        return texto.ToString();
    }

    // ─────────────────────────── historial ───────────────────────────

    // indiceHistorial == historial.Count significa "estoy escribiendo una linea nueva"
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

    // un pegado multilinea se aplana: cada salto se vuelve un espacio, no dispara varios comandos de golpe
    public static string AplanarParaPegar(string texto) =>
        texto.Replace("\r\n", " ").Replace('\n', ' ').Replace('\r', ' ').Replace('\t', ' ');

    // ─────────────────────────── autocompletado ───────────────────────────

    public (string Texto, int Caret) Autocompletar(string entrada, int caret)
    {
        int inicioToken = caret == 0 ? 0 : entrada.LastIndexOf(' ', caret - 1) + 1;
        string token = entrada[inicioToken..caret];

        // el token puede traer carpetas adelante ("src/Pane"): solo se completa el ultimo tramo
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

        // si hay varias opciones y no se puede avanzar mas, se listan, como hace bash
        if (candidatos.Count > 1 && comun.Length == parcial.Length)
        {
            Escribir(entrada, TerminalColorLinea.Escrito, PromptActual());
            foreach (var nombre in candidatos) Escribir("  " + nombre, TerminalColorLinea.Aviso);
            return (entrada, caret);
        }

        string completado = comun;
        if (candidatos.Count == 1)
        {
            // la barra final avisa que es carpeta y deja seguir bajando con otro Tab
            completado += Directory.Exists(Path.Combine(carpeta, candidatos[0])) ? Path.DirectorySeparatorChar.ToString() : " ";
        }

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
            prefijo = ""; // el prompt solo va en la primera de las lineas
        }

        while (LineasTerminal.Count > LimiteLineas) LineasTerminal.RemoveAt(0);
    }

    void AgregarError(string texto)
    {
        // la pestana arranca con un texto de relleno: se va en cuanto hay errores de verdad
        if (!_huboErroresReales)
        {
            LineasErrores.Clear();
            _huboErroresReales = true;
        }

        LineasErrores.Add(new TerminalLinea("", texto, TerminalColorLinea.Error));
        while (LineasErrores.Count > LimiteLineas) LineasErrores.RemoveAt(0);
    }

    // ─────────────────────────── prompt y rutas ───────────────────────────

    // al abrir un archivo el prompt pasa a esa carpeta, como el cwd de una terminal real
    public void RegistrarAperturaArchivo(string rutaCompleta)
    {
        string carpeta = Path.GetDirectoryName(rutaCompleta) ?? "";
        if (carpeta.Length > 0) EstablecerDirectorio(carpeta);
    }

    // se usa tambien al abrir una carpeta desde el explorador, para que la terminal quede parada ahi
    public void EstablecerDirectorio(string ruta)
    {
        if (!Directory.Exists(ruta)) return;
        _directorioAnterior = _directorioActual;
        _directorioActual = Path.GetFullPath(ruta);
        OnPropertyChanged(nameof(PrefijoEntrada));
    }

    // mientras hay un proceso corriendo la linea viva es su stdin, no un comando: va sin prompt
    public string PrefijoEntrada => _proceso.ProcesoActivo ? "" : PromptActual();

    // usuario@host fijos, la ruta sigue al directorio actual
    public string PromptActual() => $"{Environment.UserName}@{Environment.MachineName.ToLowerInvariant()}:{RutaCorta(_directorioActual)}$ ";

    // reemplaza el home real por "~"
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

    // corta el proceso que haya quedado vivo al cerrar la app, para no dejarlo huerfano
    public void Cerrar() => _proceso.Cerrar();
}
