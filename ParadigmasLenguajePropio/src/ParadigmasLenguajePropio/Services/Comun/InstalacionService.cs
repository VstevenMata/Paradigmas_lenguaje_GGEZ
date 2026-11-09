using System.Formats.Tar;
using System.IO.Compression;
using System.Reflection;
using Avalonia.Platform;

namespace ParadigmasLenguajePropio.Services;

// El ejecutable unico (publicar.sh) trae adentro un Python portatil y el traductor; aca se despliegan en la carpeta de datos
public static class InstalacionService
{
    static readonly string CarpetaDatos = Path.Combine(
        // Create: sin eso, si ~/.config no existe .NET devuelve "" y todo cae en rutas relativas
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData, Environment.SpecialFolderOption.Create), "ParadigmasLenguajePropio");
    static readonly Assembly Ensamblado = typeof(InstalacionService).Assembly;
    static readonly object Candado = new();

    // Cambia en cada compilacion: una version nueva del IDE vuelve a desplegar su traductor
    static readonly string VersionTraductor = Ensamblado.ManifestModule.ModuleVersionId.ToString();

    static string MarcaPython => Path.Combine(CarpetaDatos, "python.version");
    static string MarcaTraductor => Path.Combine(CarpetaDatos, "Traductor.version");

    // Solo lo publicado con publicar.sh trae el Python adentro; en desarrollo se usa el del sistema
    public static bool EsEjecutableUnico => Ensamblado.GetManifestResourceInfo("python.tar.gz") != null;

    public static bool FaltaDesplegar => EsEjecutableUnico && (Leer(MarcaPython) != VersionPython() || Leer(MarcaTraductor) != VersionTraductor);

    // Comando que corre ggezc.py (sin argumentos), o null si no hay traductor.
    // -X utf8: en Windows Python escribe en cp1252 hacia un pipe y la terminal mostraria mal los acentos
    public static async Task<string?> ComandoTraductorAsync()
    {
        if (!EsEjecutableUnico) return BuscarEnRepo() is { } script ? $"python -X utf8 \"{script}\"" : null;

        await Task.Run(Desplegar);
        string python = OperatingSystem.IsWindows()
            ? Path.Combine(CarpetaDatos, "python", "python.exe")
            : Path.Combine(CarpetaDatos, "python", "bin", "python3");
        return $"\"{python}\" -X utf8 \"{Path.Combine(CarpetaDatos, "Traductor", "ggezc.py")}\"";
    }

    // Desarrollo: sube desde bin/Debug/netX/ hasta encontrar Traductor/ggezc.py del repo
    static string? BuscarEnRepo()
    {
        for (var carpeta = new DirectoryInfo(AppContext.BaseDirectory); carpeta != null; carpeta = carpeta.Parent)
        {
            string candidato = Path.Combine(carpeta.FullName, "Traductor", "ggezc.py");
            if (File.Exists(candidato)) return candidato;
        }
        return null;
    }

    // Se extrae a una carpeta temporal y luego se renombra: un corte a la mitad no deja un Python roto
    static void Desplegar()
    {
        lock (Candado)
        {
            Directory.CreateDirectory(CarpetaDatos);

            if (Leer(MarcaPython) != VersionPython())
            {
                string temporal = Path.Combine(CarpetaDatos, "python.nuevo");
                BorrarSiExiste(temporal);
                Directory.CreateDirectory(temporal);
                using (var gzip = new GZipStream(Ensamblado.GetManifestResourceStream("python.tar.gz")!, CompressionMode.Decompress))
                    TarFile.ExtractToDirectory(gzip, temporal, overwriteFiles: true);
                // El .tar.gz de python-build-standalone trae todo dentro de una carpeta "python/"
                string destino = Path.Combine(CarpetaDatos, "python");
                BorrarSiExiste(destino);
                Directory.Move(Path.Combine(temporal, "python"), destino);
                BorrarSiExiste(temporal);
                File.WriteAllText(MarcaPython, VersionPython());
            }

            if (Leer(MarcaTraductor) != VersionTraductor)
            {
                string destino = Path.Combine(CarpetaDatos, "Traductor");
                BorrarSiExiste(destino);
                foreach (string nombre in Ensamblado.GetManifestResourceNames().Where(n => n.StartsWith("Traductor/")))
                {
                    string ruta = Path.Combine(CarpetaDatos, nombre);
                    Directory.CreateDirectory(Path.GetDirectoryName(ruta)!);
                    using var origen = Ensamblado.GetManifestResourceStream(nombre)!;
                    using var archivo = File.Create(ruta);
                    origen.CopyTo(archivo);
                }
                File.WriteAllText(MarcaTraductor, VersionTraductor);
            }
        }
    }

    // El tamaño del recurso alcanza para saber si cambio la version de Python embebida
    static string VersionPython()
    {
        using var recurso = Ensamblado.GetManifestResourceStream("python.tar.gz");
        return recurso?.Length.ToString() ?? "";
    }

    static string? Leer(string ruta) => File.Exists(ruta) ? File.ReadAllText(ruta) : null;

    static void BorrarSiExiste(string carpeta)
    {
        if (Directory.Exists(carpeta)) Directory.Delete(carpeta, recursive: true);
    }

    // Entrada del menu de aplicaciones de Linux; se reescribe si el ejecutable cambio de lugar
    public static void CrearAccesoLinux()
    {
        if (!OperatingSystem.IsLinux() || !EsEjecutableUnico || Environment.ProcessPath is not { } ejecutable) return;
        try
        {
            Directory.CreateDirectory(CarpetaDatos);
            string icono = Path.Combine(CarpetaDatos, "ggez.png");
            using (var origen = AssetLoader.Open(new Uri("avares://ParadigmasLenguajePropio/Assets/Icons/Icon_logo.png")))
            using (var archivo = File.Create(icono))
                origen.CopyTo(archivo);

            string datos = Environment.GetEnvironmentVariable("XDG_DATA_HOME") is { Length: > 0 } xdg
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share");
            string acceso = Path.Combine(datos, "applications", "ggez.desktop");
            string contenido = $"""
                [Desktop Entry]
                Type=Application
                Name=GGEZ
                Comment=Un lenguaje de programación con alma de videojuego
                Exec="{ejecutable}"
                Icon={icono}
                Terminal=false
                Categories=Development;IDE;

                """;
            if (Leer(acceso) == contenido) return;
            Directory.CreateDirectory(Path.GetDirectoryName(acceso)!);
            File.WriteAllText(acceso, contenido);
        }
        // Sin acceso al menu el IDE igual funciona
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
    }
}
