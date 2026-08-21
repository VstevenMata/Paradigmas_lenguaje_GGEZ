namespace ParadigmasLenguajePropio.UI;

public class NodoArchivos
{
    public void Expandir()
    {
        if (!EsCarpeta || intentoCarga || string.IsNullOrEmpty(RutaCompleta)) return;
        intentoCarga = true;

        try
        {
            foreach (var ruta in Directory.GetDirectories(RutaCompleta).OrderBy(r => r))
                Hijos.Add(CarpetaDesdeDisco(ruta));

            foreach (var ruta in Directory.GetFiles(RutaCompleta).OrderBy(r => r))
                Hijos.Add(Archivo(Path.GetFileName(ruta), ruta));
        }
        catch (Exception)
        {
            // evita que falle si la carpeta no tiene permisos o hijos legibles
        }
    }

    public static NodoArchivos Carpeta(string nombre, params NodoArchivos[] hijos) => new()
    {
        Nombre = nombre,
        EsCarpeta = true,
        Expandido = true,
        Hijos = hijos.ToList(),
        intentoCarga = true, // ya viene con los hijos puestos, no hace falta leerlos de nuevo
    };

    public static NodoArchivos Archivo(string nombre, string rutaCompleta = "") => new()
    {
        Nombre = nombre,
        RutaCompleta = rutaCompleta,
    };

    // los hijos se leen recién al expandir por primera vez (lazy load)
    public static NodoArchivos CarpetaDesdeDisco(string rutaCompleta) => new()
    {
        Nombre = Path.GetFileName(rutaCompleta.TrimEnd(Path.DirectorySeparatorChar)),
        RutaCompleta = rutaCompleta,
        EsCarpeta = true,
    };

    public string Nombre = "";
    public string RutaCompleta = "";
    public bool EsCarpeta;
    public bool Expandido;
    public List<NodoArchivos> Hijos = new();

    bool intentoCarga;
}
