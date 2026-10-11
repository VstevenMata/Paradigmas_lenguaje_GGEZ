using System.Text.Json;
using ParadigmasLenguajePropio.Models;

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
    // Mision -> huellas de los programas distintos que la cumplieron
    public Dictionary<string, HashSet<string>> Misiones { get; set; } = new();
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

    public static int EstrellasTotales =>
        _actual.Niveles.Values.Sum(n => n.Record) + Mundo.Zonas.Count(ZonaCompleta) * Mundo.EstrellasPorZona;

    // Se derivan de los records: repetir un nivel no da monedas de mas
    public static int Monedas => EstrellasTotales - _actual.MonedasGastadas;

    public static ProgresoNivel Nivel(string carpeta)
    {
        if (!_actual.Niveles.TryGetValue(carpeta, out var nivel))
            _actual.Niveles[carpeta] = nivel = new ProgresoNivel();
        return nivel;
    }

    public static int Cuenta(Mision mision) =>
        _actual.Misiones.TryGetValue(mision.Id, out var huellas) ? Math.Min(huellas.Count, mision.Veces) : 0;

    public static bool MisionCompleta(Mision mision) => Cuenta(mision) >= mision.Veces;

    public static bool ZonaCompleta(Zona zona) => zona.Misiones.All(MisionCompleta);

    // Primera zona sin completar; == Zonas.Count cuando ya se gano todo
    public static int ZonaActual
    {
        get
        {
            int i = 0;
            while (i < Mundo.Zonas.Count && ZonaCompleta(Mundo.Zonas[i])) i++;
            return i;
        }
    }

    // Cuenta el programa en todas las misiones que cumple, aunque sean de zonas mas adelante; devuelve las que avanzaron
    public static List<Mision> RegistrarPrograma(string huella, IReadOnlySet<string> usos)
    {
        var avanzadas = new List<Mision>();
        foreach (var mision in Mundo.Zonas.SelectMany(z => z.Misiones))
        {
            if (MisionCompleta(mision) || !mision.LaCumple(usos)) continue;
            if (!_actual.Misiones.TryGetValue(mision.Id, out var huellas))
                _actual.Misiones[mision.Id] = huellas = new();
            if (huellas.Add(huella)) avanzadas.Add(mision);
        }
        return avanzadas;
    }

    // Tramos recorridos en la zona: una mision de 2 programas con 1 hecho suma medio tramo
    public static double Avance(Zona zona) => zona.Misiones.Sum(m => (double)Cuenta(m) / m.Veces);

    // Reinicia solo el mundo; logros, niveles y monedas gastadas quedan
    public static void ReiniciarMundo() => _actual.Misiones.Clear();

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
            progreso.Misiones = (progreso.Misiones ?? new())
                .Where(par => par.Value != null)
                .ToDictionary(par => par.Key, par => par.Value);
            return progreso;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo leer el progreso guardado: {ex.Message}");
            return new Progreso();
        }
    }
}
