using System.Diagnostics;
using System.Runtime.InteropServices;
using Avalonia;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio;

class Program
{
    static readonly string[] SubcomandosTraductor = ["play", "run", "compile", "check", "tokens"];

    [STAThread]
    public static int Main(string[] args)
    {
        // La consola de Windows no usa UTF-8 por defecto y los acentos salian rotos
        if (OperatingSystem.IsWindows()) Console.OutputEncoding = System.Text.Encoding.UTF8;
        if (args.Length > 0 && SubcomandosTraductor.Contains(args[0])) return Traductor(args);
        if (args is ["game-over"]) return DesinstalacionService.Ejecutar();

        // En Windows el exe es de consola para que ggez play funcione; abierto con doble clic se suelta esa consola
        if (OperatingSystem.IsWindows() && GetConsoleProcessList(new uint[2], 2) == 1) FreeConsole();
        BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        return 0;
    }

    // ggez play / ggez run x.ggez: se pasa al traductor que viene adentro, sin abrir la ventana
    static int Traductor(string[] args)
    {
        if (InstalacionService.TraductorAsync().GetAwaiter().GetResult() is not var (python, script))
        {
            Console.Error.WriteLine("ggez: no se encontró el traductor");
            return 2;
        }
        // Ctrl+C le llega a los dos procesos: lo maneja Python, este solo espera
        Console.CancelKeyPress += (_, e) => e.Cancel = true;
        var info = new ProcessStartInfo(python) { UseShellExecute = false };
        foreach (string argumento in (string[])["-X", "utf8", script, .. args]) info.ArgumentList.Add(argumento);
        using var proceso = Process.Start(info)!;
        proceso.WaitForExit();
        return proceso.ExitCode;
    }

    [DllImport("kernel32.dll")] static extern uint GetConsoleProcessList(uint[] lista, uint cantidad);
    [DllImport("kernel32.dll")] static extern bool FreeConsole();

    public static AppBuilder BuildAvaloniaApp() => AppBuilder.Configure<App>()
        .UsePlatformDetect()
        .LogToTrace();
}
