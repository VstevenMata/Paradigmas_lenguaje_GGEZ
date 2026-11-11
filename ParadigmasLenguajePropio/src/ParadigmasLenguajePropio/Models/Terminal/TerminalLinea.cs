namespace ParadigmasLenguajePropio.Models;

// Salida = lo que imprime el programa; Escrito = lo que teclea el usuario
public enum TerminalColorLinea { Prompt, Escrito, Error, Aviso, Salida }

public record TerminalLinea(string Prefijo, string Texto, TerminalColorLinea Color);
