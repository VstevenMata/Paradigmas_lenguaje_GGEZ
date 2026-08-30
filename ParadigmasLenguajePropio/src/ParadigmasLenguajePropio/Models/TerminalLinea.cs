namespace ParadigmasLenguajePropio.Models;

public enum TerminalColorLinea { Prompt, Escrito, Error, Aviso }

public record TerminalLinea(string Prefijo, string Texto, TerminalColorLinea Color);
