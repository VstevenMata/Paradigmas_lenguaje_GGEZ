using System.Collections.Concurrent;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ParadigmasLenguajePropio.Services;

// Extraido casi textual de PanelTerminal.cs: proceso hijo real por comando, lectura async de
// stdout/stderr byte a byte, decodificacion UTF-8-con-fallback-a-pagina-OEM, y stripping de ANSI.
// Nada de esto depende de Avalonia (ni dependia de Raylib en el original) - es logica de proceso pura.
public class TerminalProcessService
{
    const int EsperaLineaParcial = 150; // ms sin datos tras los cuales se muestra una linea a medio escribir
    const int AnchoTabulacion = 4;

    static readonly Regex SecuenciasAnsi = new("\\[[0-9;?]*[ -/]*[@-~]", RegexOptions.Compiled);
    static readonly UTF8Encoding Utf8Estricto = new(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true);
    static readonly Encoding PaginaOem = CargarPaginaOem();

    readonly ConcurrentQueue<(string Texto, bool EsError)> _salidaPendiente = new();
    Process? _proceso;
    int _streamsCerrados;
    bool _cortadoPorUsuario;

    public bool ProcesoActivo => _proceso != null;
    public bool HayPendientes => !_salidaPendiente.IsEmpty;

    // devuelve null si el proceso arrancó bien, o el mensaje de error si no se pudo ejecutar
    public string? Lanzar(string comando, string directorioActual)
    {
        var info = new ProcessStartInfo
        {
            WorkingDirectory = directorioActual,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            RedirectStandardInput = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        // a proposito no se fija StandardOutputEncoding: los streams se leen en bytes y cada
        // linea se decodifica por su cuenta, porque no todas vienen en la misma codificacion
        if (OperatingSystem.IsWindows())
        {
            info.FileName = "cmd.exe";
            info.Arguments = $"/d /s /c \"{comando}\"";
        }
        else
        {
            info.FileName = "/bin/sh";
            info.ArgumentList.Add("-c");
            info.ArgumentList.Add(comando);
        }

        try
        {
            var proceso = new Process { StartInfo = info };

            _streamsCerrados = 0;
            proceso.Start();
            LeerStream(proceso.StandardOutput.BaseStream, esError: false);
            LeerStream(proceso.StandardError.BaseStream, esError: true);
            _proceso = proceso;
            return null;
        }
        catch (Exception ex)
        {
            return $"No se pudo ejecutar el comando: {ex.Message}";
        }
    }

    // corre fuera del hilo de la UI: por eso la cola es concurrente
    void LeerStream(Stream stream, bool esError)
    {
        _ = Task.Run(async () =>
        {
            var buffer = new byte[4096];
            var linea = new List<byte>();

            try
            {
                var lectura = stream.ReadAsync(buffer, 0, buffer.Length);
                while (true)
                {
                    // si el proceso se queda callado a mitad de linea suele ser un prompt esperando
                    // respuesta: se muestra igual, en vez de quedar invisible hasta que el comando termine
                    if (await Task.WhenAny(lectura, Task.Delay(EsperaLineaParcial)) != lectura)
                    {
                        if (linea.Count > 0)
                        {
                            _salidaPendiente.Enqueue((Decodificar(linea), esError));
                            linea.Clear();
                        }
                        continue;
                    }

                    int leidos = await lectura;
                    if (leidos == 0) break;

                    for (int i = 0; i < leidos; i++)
                    {
                        if (buffer[i] == (byte)'\n')
                        {
                            _salidaPendiente.Enqueue((Decodificar(linea), esError));
                            linea.Clear();
                        }
                        else if (buffer[i] != (byte)'\r')
                        {
                            linea.Add(buffer[i]);
                        }
                    }

                    lectura = stream.ReadAsync(buffer, 0, buffer.Length);
                }
            }
            catch
            {
                // el stream se corta solo cuando se mata el proceso: no es un error que valga mostrar
            }

            if (linea.Count > 0) _salidaPendiente.Enqueue((Decodificar(linea), esError));
            Interlocked.Increment(ref _streamsCerrados);
        });
    }

    // en Windows conviven dos codificaciones: cmd.exe escribe sus propios mensajes en la pagina OEM
    // (850/437) mientras que casi todo lo demas ya emite UTF-8. Se prueba UTF-8 estricto primero
    // porque una secuencia UTF-8 valida practicamente no aparece por casualidad en texto OEM.
    static string Decodificar(List<byte> bytes)
    {
        var crudos = bytes.ToArray();
        try { return Utf8Estricto.GetString(crudos); }
        catch (DecoderFallbackException) { return PaginaOem.GetString(crudos); }
    }

    static Encoding CargarPaginaOem()
    {
        try
        {
            Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
            return Encoding.GetEncoding(CultureInfo.CurrentCulture.TextInfo.OEMCodePage);
        }
        catch
        {
            return Encoding.Latin1; // no deberia pasar, pero mejor texto raro que una excepcion
        }
    }

    public bool TryDequeue(out (string Texto, bool EsError) salida) => _salidaPendiente.TryDequeue(out salida);

    // saca los codigos de color ANSI y los caracteres de control, que aca no significan nada
    public static string Limpiar(string texto)
    {
        texto = SecuenciasAnsi.Replace(texto, "");

        var limpio = new StringBuilder(texto.Length);
        foreach (char c in texto)
        {
            if (c == '\t') limpio.Append(' ', AnchoTabulacion);
            else if (!char.IsControl(c)) limpio.Append(c);
        }
        return limpio.ToString();
    }

    // el proceso recien termino cuando salio Y ademas cerro sus dos streams: si no, todavia
    // pueden estar viajando lineas de salida por los eventos asincronos
    public bool RevisarProcesoTerminado(out int codigoSalida, out bool cortadoPorUsuario)
    {
        codigoSalida = 0;
        cortadoPorUsuario = false;
        if (_proceso == null || !_proceso.HasExited) return false;
        if (Volatile.Read(ref _streamsCerrados) < 2 || !_salidaPendiente.IsEmpty) return false;

        codigoSalida = _proceso.ExitCode;
        _proceso.Dispose();
        _proceso = null;

        cortadoPorUsuario = _cortadoPorUsuario;
        _cortadoPorUsuario = false;
        return true;
    }

    public string? EnviarEntrada(string linea)
    {
        if (_proceso == null) return null;
        try { _proceso.StandardInput.WriteLine(linea); return null; }
        catch (Exception ex) { return $"No se pudo enviar la entrada al proceso: {ex.Message}"; }
    }

    // sin esto, un programa que lee hasta el fin de la entrada (cat, findstr, sort) no terminaria nunca
    public string? CerrarEntradaEstandar()
    {
        if (_proceso == null) return null;
        try { _proceso.StandardInput.Close(); return null; }
        catch (Exception ex) { return $"No se pudo cerrar la entrada: {ex.Message}"; }
    }

    // el arbol entero: el shell suele lanzar a su vez el programa real
    public string? Interrumpir()
    {
        if (_proceso == null) return null;
        _cortadoPorUsuario = true;
        try { _proceso.Kill(entireProcessTree: true); return null; }
        catch (Exception ex) { return $"No se pudo cortar el proceso: {ex.Message}"; }
    }

    // corta el proceso que haya quedado vivo al cerrar la app, para no dejarlo huerfano
    public void Cerrar()
    {
        if (_proceso == null) return;
        try { _proceso.Kill(entireProcessTree: true); } catch { /* ya estaba muerto */ }
        _proceso.Dispose();
        _proceso = null;
    }
}
