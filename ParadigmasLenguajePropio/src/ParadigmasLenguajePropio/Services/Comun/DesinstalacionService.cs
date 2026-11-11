using System.Diagnostics;

namespace ParadigmasLenguajePropio.Services;

// ggez game-over: deshace lo que hicieron instalar.sh / instalar.ps1 y lo que la app creo al usarse.
// Los proyectos .ggez de la persona no se tocan.
public static class DesinstalacionService
{
    static readonly string Casa = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    public static int Ejecutar()
    {
        string? exe = InstalacionService.EsEjecutableUnico ? Environment.ProcessPath : null;
        bool enNix = exe?.StartsWith("/nix/store/") == true;
        if (enNix) exe = null;   // la store de nix es de solo lectura: lo quita nix profile remove

        var carpetas = new List<string> { InstalacionService.CarpetaDatos };
        carpetas.AddRange(CarpetasExtraccion());
        var archivos = new List<string>();
        if (OperatingSystem.IsWindows())
            archivos.Add(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), @"Microsoft\Windows\Start Menu\Programs\GGEZ.lnk"));
        else
        {
            string datos = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg ? xdg : Path.Combine(Casa, ".local", "share");
            archivos.Add(Path.Combine(datos, "applications", "ggez.desktop"));
            archivos.Add(Path.Combine(Casa, ".config", "fish", "conf.d", "ggez.fish"));
            // Enlace que crea instalar.sh cuando se guardo en otra carpeta
            string enlace = Path.Combine(Casa, ".local", "bin", "ggez");
            if (new FileInfo(enlace).LinkTarget != null) archivos.Add(enlace);
        }

        var existentes = carpetas.Where(Directory.Exists).Concat(archivos.Where(Path.Exists)).ToList();
        if (exe != null) existentes.Insert(0, exe);
        if (existentes.Count == 0)
        {
            Console.WriteLine("No hay nada de GGEZ para borrar.");
            return 0;
        }

        Console.WriteLine("GAME OVER: esto borra GGEZ de esta computadora:");
        foreach (string ruta in existentes) Console.WriteLine($"  - {ruta}");
        Console.WriteLine("Se pierden los ajustes, el progreso y los logros. Tus archivos .ggez no se tocan.");
        Console.Write("¿Seguro? [s/N] ");
        if (Console.ReadLine()?.Trim().ToLowerInvariant() is not ("s" or "si" or "sí" or "y" or "yes"))
        {
            Console.WriteLine("Cancelado, no se borró nada.");
            return 1;
        }

        bool todoBien = true;
        foreach (string carpeta in carpetas.Where(Directory.Exists)) todoBien &= Borrar(carpeta, () => Directory.Delete(carpeta, recursive: true));
        foreach (string archivo in archivos.Where(Path.Exists)) todoBien &= Borrar(archivo, () => File.Delete(archivo));
        if (OperatingSystem.IsWindows() && exe != null) QuitarDelPath(Path.GetDirectoryName(exe)!);

        if (exe != null)
        {
            if (OperatingSystem.IsWindows()) BorrarAlSalir(exe);
            else
            {
                todoBien &= Borrar(exe, () => File.Delete(exe));
                // La carpeta elegida en el instalador, si quedo vacia; ~/.local/bin la usan otros programas
                string carpeta = Path.GetDirectoryName(exe)!;
                if (carpeta != Path.Combine(Casa, ".local", "bin") && Directory.Exists(carpeta) && !Directory.EnumerateFileSystemEntries(carpeta).Any())
                    Borrar(carpeta, () => Directory.Delete(carpeta));
            }
        }
        if (enNix) Console.WriteLine("Para quitar el paquete de nix: nix profile list y luego nix profile remove <nombre>");
        Console.WriteLine(todoBien ? "GG. GGEZ se desinstaló." : "Algunas cosas no se pudieron borrar; están arriba con ✖.");
        return todoBien ? 0 : 1;
    }

    static bool Borrar(string ruta, Action accion)
    {
        try
        {
            accion();
            Console.WriteLine($"  ✔ {ruta}");
            return true;
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Console.WriteLine($"  ✖ {ruta}: {e.Message}");
            return false;
        }
    }

    // Donde .NET extrae las librerias nativas del ejecutable unico: una carpeta con el nombre del ejecutable
    static IEnumerable<string> CarpetasExtraccion()
    {
        string bases = Environment.GetEnvironmentVariable("DOTNET_BUNDLE_EXTRACT_BASE_DIR") is { Length: > 0 } b
            ? b
            : OperatingSystem.IsWindows() ? Path.Combine(Path.GetTempPath(), ".net") : Path.Combine(Casa, ".net");
        string actual = Path.GetFileNameWithoutExtension(Environment.ProcessPath ?? "ggez");
        // ggez (instalar.sh), GGEZ (dist/ o Windows) y el nombre del proyecto en desarrollo
        return new[] { actual, "ggez", "GGEZ", "ParadigmasLenguajePropio" }
            .Distinct(OperatingSystem.IsWindows() ? StringComparer.OrdinalIgnoreCase : StringComparer.Ordinal)
            .Select(nombre => Path.Combine(bases, nombre));
    }

    static void QuitarDelPath(string carpeta)
    {
        string actual = Environment.GetEnvironmentVariable("Path", EnvironmentVariableTarget.User) ?? "";
        var restantes = actual.Split(';', StringSplitOptions.RemoveEmptyEntries)
            .Where(p => !p.TrimEnd('\\').Equals(carpeta.TrimEnd('\\'), StringComparison.OrdinalIgnoreCase));
        Environment.SetEnvironmentVariable("Path", string.Join(';', restantes), EnvironmentVariableTarget.User);
        Console.WriteLine($"  ✔ {carpeta} quitada del PATH");
    }

    // Windows no deja borrar un .exe que esta corriendo: cmd lo borra (y su carpeta si queda vacia) apenas este termine
    static void BorrarAlSalir(string exe)
    {
        string carpeta = Path.GetDirectoryName(exe)!;
        Process.Start(new ProcessStartInfo("cmd.exe", $"/c ping -n 3 127.0.0.1 >nul & del /f /q \"{exe}\" & rmdir \"{carpeta}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        });
        Console.WriteLine($"  ✔ {exe} (se borra al cerrar)");
    }
}
