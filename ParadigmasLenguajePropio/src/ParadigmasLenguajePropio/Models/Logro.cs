namespace ParadigmasLenguajePropio.Models;

public record Logro(string Id, string Nombre, string Descripcion);

// Ids que manda el traductor (@ggez logro <id>) mas los que detecta el IDE; un id desconocido se ignora
public static class Logros
{
    public static readonly IReadOnlyList<Logro> Todos =
    [
        new("primer-programa", "GG fácil", "Correr un programa sin errores"),
        new("combo", "Combo inicial", "Declarar un combo propio"),
        new("recursion", "Recursivo", "Un combo que se llama a sí mismo"),
        new("squad", "Squad armado", "Usar una lista squad"),
        new("loadout", "Loadout listo", "Usar loadout (switch)"),
        new("tres-ciclos", "Grinder", "Usar farm, survive y grind en un mismo programa"),
        new("clutch", "Clutch", "Atrapar un error con clutch / revive"),
        new("guild", "Fundador", "Declarar un guild (clase)"),
        new("herencia", "Evolución", "Un guild que evolves de otro"),
        new("modulos", "Viajero", "Traer otro archivo con travel"),
        new("sin-mana", "Sin maná", "Quedarse sin maná por un ciclo sin salida"),
        new("nivel-perfecto", "Perfecto", "Ganar las 3 estrellas en un nivel"),
        new("primera-pista", "Pedir ayuda", "Comprar una pista"),
    ];

    public static Logro? Buscar(string id) => Todos.FirstOrDefault(l => l.Id == id);
}
