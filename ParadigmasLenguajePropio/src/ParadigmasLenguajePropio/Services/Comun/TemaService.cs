using System.Text.Json;
using Avalonia;
using Avalonia.Markup.Xaml.Styling;

namespace ParadigmasLenguajePropio.Services;

public enum Tema { Win95, Zelda, Sonic, MegaMan }

// Cambia la paleta de colores incluida en App.axaml en caliente, sin reiniciar la ventana.
public static class TemaService
{
    // Indice del StyleInclude de la paleta en Application.Styles (App.axaml)
    const int IndicePaleta = 1;

    static readonly string RutaArchivo = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "ParadigmasLenguajePropio", "tema.json");

    public static Tema Actual { get; private set; } = Tema.Win95;

    // Los controles dibujados a mano (el mundo del panel de juego) no se repintan solos con la paleta nueva
    public static event Action? Cambio;

    public static void Aplicar(Tema tema)
    {
        if (Application.Current is { } app)
        {
            string archivo = tema switch
            {
                Tema.Win95 => "Win95Paleta",
                Tema.Zelda => "ZeldaPaleta",
                Tema.Sonic => "SonicPaleta",
                Tema.MegaMan => "MegaManPaleta",
                _ => "Win95Paleta",
            };

            app.Styles[IndicePaleta] = new StyleInclude(new Uri("avares://ParadigmasLenguajePropio/"))
            {
                Source = new Uri($"avares://ParadigmasLenguajePropio/Themes/Paletas/{archivo}.axaml")
            };
        }

        Actual = tema;
        Guardar(tema);
        Cambio?.Invoke();
    }

    // El tema activo se guarda aparte de las extensiones instaladas: instalado != activo
    public static Tema CargarGuardado()
    {
        try
        {
            if (!File.Exists(RutaArchivo)) return Tema.Win95;
            var guardado = JsonSerializer.Deserialize<string>(File.ReadAllText(RutaArchivo));
            return Enum.TryParse<Tema>(guardado, out var tema) ? tema : Tema.Win95;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo leer el tema guardado: {ex.Message}");
            return Tema.Win95;
        }
    }

    static void Guardar(Tema tema)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(RutaArchivo)!);
            File.WriteAllText(RutaArchivo, JsonSerializer.Serialize(tema.ToString()));
        }
        catch (Exception ex)
        {
            Console.WriteLine($"No se pudo guardar el tema: {ex.Message}");
        }
    }
}
