using System.Collections.Generic;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.Models;

// La extension ya esta compilada en la app: "instalar" solo prende Instalada
public partial class ExtensionInfo : ObservableObject
{
    public string Id { get; }
    public string Nombre { get; }
    public string Autor { get; }

    // Sigla en vez de emoji: las fuentes del tema no traen glifos de emoji (salian como tofu)
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
