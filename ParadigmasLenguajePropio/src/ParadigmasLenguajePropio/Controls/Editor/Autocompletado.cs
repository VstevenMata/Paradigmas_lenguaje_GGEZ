using System.IO;
using System.Text.RegularExpressions;

namespace ParadigmasLenguajePropio.Controls;

// Plantilla: texto que se inserta ($ = donde queda el cursor). Estructura: guia que solo se muestra
public record Sugerencia(string Texto, string Detalle, string? Plantilla = null, string? Estructura = null);

// Sugerencias del editor sacadas del texto con regex; no corre el traductor
public static class Autocompletado
{
    const string Identificador = @"[A-Za-z_][A-Za-z0-9_]*";
    const string Tipo = @"(?:squad\[[^\]\n]*\]|[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)?)";

    static readonly Regex RegexGuild = new(@"^[ \t]*guild[ \t]+(" + Identificador + @")(?:[ \t]+evolves[ \t]+(" + Identificador + @"))?", RegexOptions.Multiline);
    static readonly Regex RegexInventory = new(@"^[ \t]*inventory[ \t]+(" + Identificador + ")", RegexOptions.Multiline);
    static readonly Regex RegexCombo = new(@"^[ \t]*(open|party|solo)?[ \t]*combo[ \t]+(" + Identificador + @")[ \t]*\{([^}\n]*)\}[ \t]*drops[ \t]+(\S+)", RegexOptions.Multiline);
    static readonly Regex RegexCampo = new(@"^[ \t]*(open|party|solo)[ \t]+(" + Tipo + @")[ \t]+(" + Identificador + @")[ \t]*(?:set\b|$)", RegexOptions.Multiline);
    static readonly Regex RegexTravel = new(@"^[ \t]*travel[ \t]+(" + Identificador + @")[ \t]+from[ \t]+(" + Identificador + ")", RegexOptions.Multiline);
    static readonly Regex RegexDeclaracion = new(@"\b(?:stash|hardcoded)[ \t]+(" + Tipo + @")[ \t]+(" + Identificador + ")");

    static readonly Sugerencia[] MiembrosChat =
    {
        new("send", "chat.send{x}: imprime en la terminal", Estructura: "chat.send{\"texto\"}"),
        new("rage", "chat.rage{x}: imprime como error", Estructura: "chat.rage{\"texto\"}"),
        new("load", "chat.load{}: lee una linea del teclado (lore)", Estructura: "stash lore linea set chat.load{}"),
    };

    static readonly Sugerencia[] MiembrosSquad =
    {
        new("size", "cantidad de elementos (score)"),
        new("recruit", "agrega al final", Estructura: "lista.recruit{valor}"),
        new("kick", "saca por indice", Estructura: "lista.kick{indice}"),
    };

    static readonly Sugerencia[] MiembrosRevive =
    {
        new("reason", "mensaje del error (lore)"),
        new("line", "linea del error (score)"),
    };

    const string PlantillaLobby = "lobby.launch{start THE GAME} glhf\n    $\ngg";

    // Lo que no esta aca igual sale como palabra reservada, solo que sin guia
    static readonly Dictionary<string, (string Detalle, string? Estructura)> Reservadas = new()
    {
        ["lobby"] = ("punto de entrada del programa", null),
        ["travel"] = ("importa otro archivo", "travel modulo from carpeta"),
        ["stash"] = ("declara una variable", "stash score nombre set 0"),
        ["hardcoded"] = ("declara una constante", "hardcoded score NOMBRE set 10"),
        ["scan"] = ("condicional (if)", "scan condicion glhf\n    ...\ngg fallback glhf\n    ...\ngg"),
        ["fallback"] = ("rama si no (else)", "gg fallback glhf\n    ...\ngg"),
        ["farm"] = ("ciclo con contador (for)", "farm i set 0 | i under 10 | i buff glhf\n    ...\ngg"),
        ["survive"] = ("ciclo mientras (while)", "survive condicion glhf\n    ...\ngg"),
        ["grind"] = ("ciclo hacer-mientras (do-while)", "grind glhf\n    ...\ngg survive {condicion}"),
        ["loadout"] = ("seleccion multiple (switch)", "loadout valor glhf\n    pick 1 | 2 glhf ... gg\n    noob glhf ... gg\ngg"),
        ["pick"] = ("caso del loadout", "pick 1 | 2 glhf\n    ...\ngg"),
        ["noob"] = ("caso por defecto del loadout", "noob glhf\n    ...\ngg"),
        ["ragequit"] = ("sale del ciclo (break)", null),
        ["respawn"] = ("salta a la siguiente vuelta (continue)", null),
        ["combo"] = ("declara una funcion", "combo nombre{score a | score b} drops score glhf\n    reward a gain b\ngg"),
        ["reward"] = ("devuelve un valor (return)", "reward valor"),
        ["drops"] = ("tipo que devuelve el combo", "drops score   /w o drops noloot"),
        ["clutch"] = ("intenta un bloque (try)", "clutch glhf\n    ...\ngg revive{e} glhf\n    chat.send{e.reason}\ngg checkpoint glhf\n    ...\ngg"),
        ["revive"] = ("atrapa el error (catch)", "gg revive{e} glhf\n    ...\ngg"),
        ["checkpoint"] = ("corre siempre (finally)", "gg checkpoint glhf\n    ...\ngg"),
        ["wipe"] = ("lanza un error (throw)", "wipe{\"mensaje\"}"),
        ["quit"] = ("termina el programa", "quit{codigo}"),
        ["guild"] = ("declara una clase", "guild Nombre evolves Padre glhf\n    solo score campo\n    combo Nombre{score x} drops noloot glhf\n        campo set x\n    gg\ngg"),
        ["evolves"] = ("hereda de otro guild", "guild Hijo evolves Padre glhf ... gg"),
        ["inventory"] = ("declara un registro", "inventory Nombre glhf\n    score campo\ngg"),
        ["spawn"] = ("crea una instancia (new)", "spawn Clase{argumentos}"),
        ["squad"] = ("vector", "stash squad[score] lista set <1 | 2 | 3>"),
        ["me"] = ("instancia actual (this)", null),
    };

    static readonly string[] OtrasReservadas =
    {
        "launch", "start", "THE", "GAME", "from", "noloot", "afk", "score", "aim", "lore", "win", "pixel",
        "hit", "miss", "open", "party", "solo", "glhf", "gg", "gain", "drain", "crit", "split", "scrap",
        "matches", "mismatch", "over", "under", "atleast", "atmost", "set", "boost", "sap", "buff", "nerf",
        "chain", "swap", "dodge",
    };

    // Devuelve la columna donde empieza lo que se va a reemplazar y las opciones; forzar = Ctrl+Espacio
    public static (int Inicio, List<Sugerencia> Opciones) Sugerir(IReadOnlyList<string> lineas, int fila, int columna, string? rutaArchivo, bool forzar = false)
    {
        string linea = lineas[fila];
        int inicio = columna;
        while (inicio > 0 && (char.IsLetterOrDigit(linea[inicio - 1]) || linea[inicio - 1] == '_')) inicio--;
        string prefijo = linea[inicio..columna];

        if (EnComentarioOCadena(lineas, fila, inicio)) return (inicio, new());

        string texto = string.Join('\n', lineas);
        List<Sugerencia> candidatos;
        if (inicio > 0 && linea[inicio - 1] == '.')
        {
            int finObjeto = inicio - 1, inicioObjeto = finObjeto;
            while (inicioObjeto > 0 && (char.IsLetterOrDigit(linea[inicioObjeto - 1]) || linea[inicioObjeto - 1] == '_')) inicioObjeto--;
            candidatos = Miembros(linea[inicioObjeto..finObjeto], texto, fila, lineas, rutaArchivo);
        }
        else
        {
            if (prefijo.Length == 0 && !forzar) return (inicio, new());
            candidatos = Palabras(texto);
        }

        var opciones = candidatos.Where(s => s.Texto.StartsWith(prefijo, StringComparison.Ordinal)).ToList();
        // Ya escribio la palabra entera y elegirla no agrega nada: no estorbar
        if (opciones.All(s => s.Texto == prefijo && s.Plantilla == null)) opciones.Clear();
        return (inicio, opciones);
    }

    static List<Sugerencia> Palabras(string texto)
    {
        var lista = new List<Sugerencia> { new("lobby", "inserta el punto de entrada", PlantillaLobby, PlantillaLobby.Replace("$", "...")) };
        foreach (var (palabra, (detalle, estructura)) in Reservadas)
            if (palabra != "lobby") lista.Add(new(palabra, detalle, Estructura: estructura));
        foreach (var palabra in OtrasReservadas) lista.Add(new(palabra, "palabra reservada"));
        lista.Add(new("chat", "modulo de entrada y salida"));

        var vistos = new HashSet<string>(lista.Select(s => s.Texto));
        void Agregar(string nombre, string detalle) { if (vistos.Add(nombre)) lista.Add(new(nombre, detalle)); }
        foreach (Match m in RegexDeclaracion.Matches(texto)) Agregar(m.Groups[2].Value, m.Groups[1].Value);
        foreach (Match m in RegexCombo.Matches(texto)) Agregar(m.Groups[2].Value, $"combo -> {m.Groups[4].Value}");
        foreach (Match m in RegexGuild.Matches(texto)) Agregar(m.Groups[1].Value, "guild");
        foreach (Match m in RegexInventory.Matches(texto)) Agregar(m.Groups[1].Value, "inventory");
        foreach (Match m in RegexTravel.Matches(texto)) Agregar(m.Groups[1].Value, "modulo importado");

        lista.Sort((a, b) => string.CompareOrdinal(a.Texto, b.Texto));
        return lista;
    }

    static List<Sugerencia> Miembros(string objeto, string texto, int fila, IReadOnlyList<string> lineas, string? rutaArchivo)
    {
        if (objeto.Length == 0) return new();
        if (objeto == "lobby")
            return new() { new("launch", "inserta el punto de entrada", PlantillaLobby["lobby.".Length..], PlantillaLobby.Replace("$", "...")) };
        if (objeto == "chat") return MiembrosChat.ToList();
        if (objeto == "me") return GuildActual(lineas, fila) is { } g ? MiembrosDeTipo(g, texto, propios: true) : new();

        var travel = RegexTravel.Matches(texto).FirstOrDefault(m => m.Groups[1].Value == objeto);
        if (travel != null) return MiembrosDeModulo(travel.Groups[1].Value, travel.Groups[2].Value, rutaArchivo);

        if (Regex.IsMatch(texto, @"\brevive\s*\{\s*" + Regex.Escape(objeto) + @"\s*\}")) return MiembrosRevive.ToList();

        // Variable, campo o parametro: el tipo es lo que va justo antes del nombre
        var tipo = Regex.Match(texto, @"(?:\b(?:stash|hardcoded|open|party|solo)[ \t]+|[{|][ \t]*)(" + Tipo + @")[ \t]+" + Regex.Escape(objeto) + @"\b") is { Success: true } d
            ? d.Groups[1].Value : null;
        if (tipo == null) return new();
        if (tipo.StartsWith("squad[")) return MiembrosSquad.ToList();

        int punto = tipo.IndexOf('.');
        if (punto < 0) return MiembrosDeTipo(tipo, texto, propios: false);

        var modulo = RegexTravel.Matches(texto).FirstOrDefault(m => m.Groups[1].Value == tipo[..punto]);
        string? textoModulo = modulo == null ? null : LeerModulo(modulo.Groups[1].Value, modulo.Groups[2].Value, rutaArchivo);
        return textoModulo == null ? new() : MiembrosDeTipo(tipo[(punto + 1)..], textoModulo, propios: false);
    }

    // Visibilidad como D-24: con me se ve todo lo propio y lo no solo del padre; desde afuera solo lo open
    static List<Sugerencia> MiembrosDeTipo(string tipo, string texto, bool propios)
    {
        bool desdeDentro = propios;
        bool Visible(string modificador) => propios || (desdeDentro ? modificador != "solo" : modificador is "open" or "");
        var lista = new List<Sugerencia>();
        var vistos = new HashSet<string>();
        for (string? actual = tipo; actual != null && vistos.Add(actual);)
        {
            var bloque = BloqueDe(texto, actual, out string? padre);
            if (bloque == null) break;
            foreach (Match m in RegexCampo.Matches(bloque))
                if (Visible(m.Groups[1].Value)) lista.Add(new(m.Groups[3].Value, $"{m.Groups[1].Value} {m.Groups[2].Value}"));
            foreach (Match m in RegexCombo.Matches(bloque))
                if (Visible(m.Groups[1].Value) && m.Groups[2].Value != actual)
                    lista.Add(new(m.Groups[2].Value, $"combo -> {m.Groups[4].Value}", Estructura: $"{m.Groups[2].Value}{{{m.Groups[3].Value}}}"));
            // Campos de inventory no llevan modificador
            if (Regex.IsMatch(texto, @"^[ \t]*inventory[ \t]+" + Regex.Escape(actual) + @"\b", RegexOptions.Multiline))
                foreach (Match m in Regex.Matches(bloque, @"^[ \t]*(" + Tipo + @")[ \t]+(" + Identificador + @")[ \t]*$", RegexOptions.Multiline))
                    lista.Add(new(m.Groups[2].Value, m.Groups[1].Value));
            propios = false;
            actual = padre;
        }
        return lista.DistinctBy(s => s.Texto).OrderBy(s => s.Texto, StringComparer.Ordinal).ToList();
    }

    // Cuerpo entre la cabecera del guild/inventory y su gg contando glhf/gg anidados
    static string? BloqueDe(string texto, string tipo, out string? padre)
    {
        padre = null;
        var cabecera = Regex.Match(texto, @"^[ \t]*(?:guild|inventory)[ \t]+" + Regex.Escape(tipo) + @"\b(?:[ \t]+evolves[ \t]+(" + Identificador + "))?.*$", RegexOptions.Multiline);
        if (!cabecera.Success) return null;
        if (cabecera.Groups[1].Success) padre = cabecera.Groups[1].Value;

        int nivel = 0, inicio = cabecera.Index + cabecera.Length;
        foreach (Match m in Regex.Matches(texto[cabecera.Index..], @"\b(glhf|gg)\b"))
        {
            nivel += m.Value == "glhf" ? 1 : -1;
            if (nivel == 0) return texto[inicio..Math.Max(inicio, cabecera.Index + m.Index)];
        }
        return texto[inicio..];
    }

    static List<Sugerencia> MiembrosDeModulo(string modulo, string carpeta, string? rutaArchivo)
    {
        string? texto = LeerModulo(modulo, carpeta, rutaArchivo);
        if (texto == null) return new();
        var lista = new List<Sugerencia>();
        // Solo combos globales: los de un guild empiezan indentados
        foreach (Match m in RegexCombo.Matches(texto))
            if (!char.IsWhiteSpace(texto[m.Index]))
                lista.Add(new(m.Groups[2].Value, $"combo -> {m.Groups[4].Value}", Estructura: $"{modulo}.{m.Groups[2].Value}{{{m.Groups[3].Value}}}"));
        foreach (Match m in RegexGuild.Matches(texto)) lista.Add(new(m.Groups[1].Value, "guild"));
        foreach (Match m in RegexInventory.Matches(texto)) lista.Add(new(m.Groups[1].Value, "inventory"));
        return lista.DistinctBy(s => s.Texto).OrderBy(s => s.Texto, StringComparer.Ordinal).ToList();
    }

    // Misma regla que el traductor (D-28): carpeta relativa al archivo, "base" = la misma
    static string? LeerModulo(string modulo, string carpeta, string? rutaArchivo)
    {
        string? dir = string.IsNullOrEmpty(rutaArchivo) ? null : Path.GetDirectoryName(rutaArchivo);
        if (dir == null) return null;
        string ruta = carpeta == "base" ? Path.Combine(dir, modulo + ".ggez") : Path.Combine(dir, carpeta, modulo + ".ggez");
        try { return File.Exists(ruta) ? File.ReadAllText(ruta) : null; }
        catch (IOException) { return null; }
    }

    // El ultimo guild abierto arriba del cursor
    static string? GuildActual(IReadOnlyList<string> lineas, int fila)
    {
        for (int f = fila; f >= 0; f--)
        {
            var m = RegexGuild.Match(lineas[f]);
            if (m.Success) return m.Groups[1].Value;
        }
        return null;
    }

    static bool EnComentarioOCadena(IReadOnlyList<string> lineas, int fila, int columna)
    {
        bool enBloque = false;
        for (int f = 0; f < fila; f++) EditorTextArea.BuscarComentarios(lineas[f], ref enBloque);
        string antes = lineas[fila][..columna];
        if (EditorTextArea.BuscarComentarios(antes + " ", ref enBloque).Any(r => r.Fin > columna)) return true;
        return antes.Count(c => c == '"') % 2 == 1;
    }
}
