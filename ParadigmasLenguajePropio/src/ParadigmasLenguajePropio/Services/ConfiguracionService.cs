using System.Text.Json;

namespace ParadigmasLenguajePropio.Services;

public record ConfiguracionUsuario(double TamanoFuente, int TamanoTab);

// persiste tamaño de fuente y ancho de tab entre sesiones, mismo patron que TemaService
public static class ConfiguracionService
{
    public const double TamanoFuentePorDefecto = 18;
    public const int TamanoTabPorDefecto = 4;

    static readonly string RutaArchivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ParadigmasLenguajePropio", "configuracion.json");

    public static ConfiguracionUsuario CargarGuardada()
    {
        try
        {
            if (!File.Exists(RutaArchivo)) return new ConfiguracionUsuario(TamanoFuentePorDefecto, TamanoTabPorDefecto);
            var config = JsonSerializer.Deserialize<ConfiguracionUsuario>(File.ReadAllText(RutaArchivo));
            return config ?? new ConfiguracionUsuario(TamanoFuentePorDefecto, TamanoTabPorDefecto);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo leer la configuracion guardada: {ex.Message}");
            return new ConfiguracionUsuario(TamanoFuentePorDefecto, TamanoTabPorDefecto);
        }
    }

    public static void Guardar(ConfiguracionUsuario config)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(config));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo guardar la configuracion: {ex.Message}");
        }
    }
}
