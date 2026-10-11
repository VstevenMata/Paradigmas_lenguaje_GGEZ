using System.Text.Json;

namespace ParadigmasLenguajePropio.Services;

// Guarda las extensiones instaladas entre sesiones en un config del perfil del usuario
public static class ExtensionesPersistenciaService
{
    static readonly string RutaArchivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ParadigmasLenguajePropio", "extensiones.json");

    public static HashSet<string> CargarInstaladas()
    {
        try
        {
            if (!File.Exists(RutaArchivo)) return new HashSet<string>();
            var ids = JsonSerializer.Deserialize<string[]>(File.ReadAllText(RutaArchivo));
            return ids is null ? new HashSet<string>() : new HashSet<string>(ids);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo leer el estado de las extensiones: {ex.Message}");
            return new HashSet<string>();
        }
    }

    public static void Guardar(IEnumerable<string> idsInstaladas)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(idsInstaladas.ToArray()));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo guardar el estado de las extensiones: {ex.Message}");
        }
    }
}
