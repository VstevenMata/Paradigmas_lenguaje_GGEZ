using System;
using System.Collections.ObjectModel;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;

namespace ParadigmasLenguajePropio.ViewModels;

public partial class StatusBarViewModel : ViewModelBase
{
    private static readonly string[] FrasesMotivacionales =
    [
        "Los científicos estudian el mundo como es, los ingenieros crean el mundo que nunca ha existido.",
        "Para ser un buen programador se requiere un 3% de talento y el 97% de esfuerzo para no distraerse con internet.",
        "If it's not fun, why do it? - Linus Torvalds",
        "Todo es posible, en la medida que tu creas que es posible.",
        "No es magia, es la capa 8.",
        "Los que pueden, lo hacen; los que no, solo saben quejarse. - Linus Torvalds",
        "Filipenses 4:13",
        "En la vida hay algo peor que el fracaso: el no haber intentado nada. - Franklin D. Roosevelt",
        "Primero resuelve el problema, después escribe el código. - John Johnson",
        "La vida es demasiado corta para quitar el USB con seguridad.",
        "Es posible robar las ideas, pero nadie puede robar su puesta en práctica ni la pasión por ellas. - Timothy Ferriss",
        "No fracasé. Solo encontré 10 mil formas que no funcionan. - Thomas Edison",
        "A veces la persona que nadie imagina capaz de nada, es la que hace cosas que nadie imagina.",
        "Solo sé que no sé nada. - Sócrates",
        "La resolución de problemas es la esencia de la programación.",
        "Si no estás haciendo algo que te asuste, probablemente no estás haciendo algo innovador. - Sam Altman",
    ];

    private static readonly Random _random = new();
    private readonly DispatcherTimer _timerFrases;

    private int _indiceFraseActual;

    [ObservableProperty]
    private string _mensaje;

    [ObservableProperty]
    private string _posicionCursor = "";

    public ObservableCollection<string> Segmentos { get; } = new(["Steven", "Maikel", "Tiso"]);

    public StatusBarViewModel()
    {
        _indiceFraseActual = _random.Next(FrasesMotivacionales.Length);
        _mensaje = FrasesMotivacionales[_indiceFraseActual];

        _timerFrases = new DispatcherTimer { Interval = TimeSpan.FromSeconds(15) };
        _timerFrases.Tick += (_, _) => Mensaje = SiguienteFraseAleatoria();
        _timerFrases.Start();
    }

    // Evita repetir la misma frase dos veces seguidas
    private string SiguienteFraseAleatoria()
    {
        int siguiente;
        do
        {
            siguiente = _random.Next(FrasesMotivacionales.Length);
        } while (siguiente == _indiceFraseActual && FrasesMotivacionales.Length > 1);

        _indiceFraseActual = siguiente;
        return FrasesMotivacionales[siguiente];
    }
}
