namespace ParadigmasLenguajePropio.Models;

public record ProblemaItem(string Archivo, int Linea, int Columna, string Mensaje, TerminalColorLinea Severidad)
{
    public string RutaCorta => System.IO.Path.GetFileName(Archivo);
    public string Ubicacion => $"{RutaCorta}:{Linea}:{Columna}";
}
