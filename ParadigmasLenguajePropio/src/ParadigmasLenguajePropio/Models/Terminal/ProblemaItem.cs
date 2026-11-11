namespace ParadigmasLenguajePropio.Models;

public record ProblemaItem(string Archivo, int Linea, int Columna, string Mensaje, TerminalColorLinea Severidad, string? Causa = null)
{
    public bool TieneCausa => !string.IsNullOrEmpty(Causa);
    public string RutaCorta => System.IO.Path.GetFileName(Archivo);
    public string Ubicacion => $"{RutaCorta}:{Linea}:{Columna}";
}
