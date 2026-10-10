using System.Text.Json;

namespace ParadigmasLenguajePropio.Services;

public class ProgresoNivel
{
    public int Intentos { get; set; }
    public int Record { get; set; }
    public int PistasCompradas { get; set; }
}

public class Progreso
{
    // Clave: carpeta absoluta del nivel
    public Dictionary<string, ProgresoNivel> Niveles { get; set; } = new();
    public int MonedasGastadas { get; set; }
    public HashSet<string> Logros { get; set; } = new();
}

// Guarda intentos, estrellas y pistas compradas entre sesiones
// ponytail: la clave es la ruta de la carpeta, mover un nivel de lugar reinicia su progreso
public static class ProgresoService
{
    static readonly string RutaArchivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ParadigmasLenguajePropio", "progreso.json");

    static readonly Progreso _actual = Cargar();

    // Lo escuchan las extensiones para desbloquear temas
    public static event Action? Cambio;

    public static int EstrellasTotales => _actual.Niveles.Values.Sum(n => n.Record);

    // Se derivan de los records: repetir un nivel no da monedas de mas
    public static int Monedas => EstrellasTotales - _actual.MonedasGastadas;

    public static ProgresoNivel Nivel(string carpeta)
    {
        if (!_actual.Niveles.TryGetValue(carpeta, out var nivel))
            _actual.Niveles[carpeta] = nivel = new ProgresoNivel();
        return nivel;
    }

    public static bool TieneLogro(string id) => _actual.Logros.Contains(id);

    // true solo la primera vez: para avisar del logro nuevo
    public static bool GanarLogro(string id) => _actual.Logros.Add(id);

    public static bool Gastar(int monedas)
    {
        if (Monedas < monedas) return false;
        _actual.MonedasGastadas += monedas;
        return true;
    }

    public static void Guardar()
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(_actual));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo guardar el progreso: {ex.Message}");
        }
        Cambio?.Invoke();
    }

    static Progreso Cargar()
    {
        try
        {
            if (!File.Exists(RutaArchivo)) return new Progreso();
            var progreso = JsonSerializer.Deserialize<Progreso>(File.ReadAllText(RutaArchivo)) ?? new Progreso();
            // Un JSON con "Niveles": null o entradas null no debe tumbar el IDE al sumar
            progreso.Niveles = (progreso.Niveles ?? new())
                .Where(par => par.Value != null)
                .ToDictionary(par => par.Key, par => par.Value);
            progreso.Logros ??= new();
            return progreso;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo leer el progreso guardado: {ex.Message}");
            return new Progreso();
        }
    }
}
