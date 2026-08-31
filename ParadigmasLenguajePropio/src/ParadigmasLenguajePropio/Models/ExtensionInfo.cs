using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.Models;

// La extension ya vive compilada en la app: "instalar" no descarga nada de internet, solo prende
// Instalada (con una animacion de progreso fantasma en el medio, ver ExtensionsViewModel).
public partial class ExtensionInfo : ObservableObject
{
    public string Id { get; }
    public string Nombre { get; }
    public string Autor { get; }

    // sigla corta (2-3 letras) sobre una placa de color, en vez de un emoji: Win95FontFamily
    // (Liberation Sans) no trae glifos de emoji y salian como una caja rota/tofu
    public string Icono { get; }
    public IBrush IconoColor { get; }

    public string DescripcionCorta { get; }
    public IReadOnlyList<string> Caracteristicas { get; }

    [ObservableProperty]
    private bool _instalada;

    [ObservableProperty]
    private bool _instalando;

    [ObservableProperty]
    private double _progreso;

    public ExtensionInfo(string id, string nombre, string autor, string icono, string colorHex, string descripcionCorta,
        IReadOnlyList<string> caracteristicas, bool instalada = false)
    {
        Id = id;
        Nombre = nombre;
        Autor = autor;
        Icono = icono;
        IconoColor = new SolidColorBrush(Color.Parse(colorHex));
        DescripcionCorta = descripcionCorta;
        Caracteristicas = caracteristicas;
        Instalada = instalada;
    }
}
