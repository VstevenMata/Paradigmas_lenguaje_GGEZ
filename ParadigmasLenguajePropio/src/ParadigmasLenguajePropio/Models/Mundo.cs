namespace ParadigmasLenguajePropio.Models;

// Requiere = usos que tiene que reportar el traductor en un mismo programa (@ggez programa <huella> <usos...>)
// Minimo = cuantos de Requiere hacen falta (0 = todos). Veces = programas distintos que la cumplen
public record Mision(string Id, string Texto, string[] Requiere, int Veces = 1, int Minimo = 0)
{
    public bool LaCumple(IReadOnlySet<string> usos)
    {
        int tiene = Requiere.Count(usos.Contains);
        return tiene >= (Minimo > 0 ? Minimo : Requiere.Length);
    }
}

public record Zona(string Nombre, IReadOnlyList<Mision> Misiones);

public static class Mundo
{
    public const int EstrellasPorZona = 3;

    public static readonly IReadOnlyList<Zona> Zonas =
    [
        new("Aldea",
        [
            new("aldea-1", "Correr un programa sin errores", []),
            new("aldea-2", "Correr 3 programas distintos", [], Veces: 3),
        ]),
        new("Pradera",
        [
            new("pradera-1", "Tomar decisiones con scan (2 programas)", ["scan"], Veces: 2),
            new("pradera-2", "Hacer que corra un fallback", ["fallback"]),
        ]),
        new("Bosque de los ciclos",
        [
            new("bosque-1", "Un farm que dé vueltas (3 programas)", ["farm"], Veces: 3),
            new("bosque-2", "Cortar un ciclo con ragequit", ["ragequit"]),
            new("bosque-3", "Un farm dentro de otro farm", ["farm-anidado"]),
        ]),
        new("Río",
        [
            new("rio-1", "Usar survive (2 programas)", ["survive"], Veces: 2),
            new("rio-2", "Usar grind", ["grind"]),
        ]),
        new("Cueva",
        [
            new("cueva-1", "Llamar a un combo propio (3 programas)", ["combo"], Veces: 3),
            new("cueva-2", "Elegir con loadout", ["loadout"]),
        ]),
        new("Torre",
        [
            new("torre-1", "Una recursión que termine (2 programas)", ["recursion"], Veces: 2),
        ]),
        new("Mercado",
        [
            new("mercado-1", "Llenar una squad con recruit (2 programas)", ["squad-recruit"], Veces: 2),
            new("mercado-2", "Leer una squad por índice y recorrerla con farm", ["squad-indice", "farm"]),
        ]),
        new("Castillo",
        [
            new("castillo-1", "Un revive que atrape un error de verdad (2 programas)", ["revive"], Veces: 2),
        ]),
        new("Gremio",
        [
            new("gremio-1", "Crear objetos de un guild (2 programas)", ["guild"], Veces: 2),
            new("gremio-2", "Un guild que evolves de otro", ["herencia"]),
        ]),
        new("Guarida del jefe",
        [
            new("jefe-1", "Usar algo de otro archivo con travel", ["travel"]),
            new("jefe-2", "Un programa que use 5 de: farm, scan, combo, recursión, squad, revive, guild",
                ["farm", "scan", "combo", "recursion", "squad-recruit", "revive", "guild"], Minimo: 5),
        ]),
    ];
}
