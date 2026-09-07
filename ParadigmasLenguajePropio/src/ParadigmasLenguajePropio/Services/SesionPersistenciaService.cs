using System.Text.Json;
using ParadigmasLenguajePropio.Models;

namespace ParadigmasLenguajePropio.Services;

public record TabSesion(string RutaCompleta, int Fila, int Columna);
public record PanelSesion(List<TabSesion> Tabs, int IndiceActivo);

// espejo serializable de NodoPanel (ver Models/NodoPanel.cs): Panel null = split con A/B, no null = hoja
public record NodoLayoutSesion(PanelSesion? Panel, OrientacionSplit Orientacion, NodoLayoutSesion? A, NodoLayoutSesion? B);

public record SesionGuardada(string CarpetaRaiz, NodoLayoutSesion? Layout, int CantidadTerminales, int TerminalActivaIndice);

// persiste carpeta, layout de splits (con cursor por pestaña) y terminales entre sesiones, mismo patron que TemaService
public static class SesionPersistenciaService
{
    static readonly string RutaArchivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ParadigmasLenguajePropio", "sesion.json");

    public static SesionGuardada? Cargar()
    {
        try
        {
            if (!File.Exists(RutaArchivo)) return null;
            return JsonSerializer.Deserialize<SesionGuardada>(File.ReadAllText(RutaArchivo));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo leer la sesion guardada: {ex.Message}");
            return null;
        }
    }

    public static void Guardar(SesionGuardada sesion)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(sesion));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo guardar la sesion: {ex.Message}");
        }
    }
}
