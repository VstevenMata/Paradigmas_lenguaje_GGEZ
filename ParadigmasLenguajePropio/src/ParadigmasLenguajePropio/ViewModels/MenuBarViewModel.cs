using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ParadigmasLenguajePropio.ViewModels;

// Reemplaza MenuBar.cs. Se abandona el Menu/MenuItem nativo de Avalonia: su apariencia de
// hover/seleccion viene de recursos internos del tema base que no se pueden sobreescribir de
// forma confiable sin tocar su Template (a diferencia de Button, que si expone eso via Background/
// TemplateBinding). Un Popup propio con Win95Bevel da control total del look, igual que el resto
// de la app.
public partial class MenuBarViewModel : ViewModelBase
{
    const string Separador = "-";

    public IReadOnlyList<MenuPrincipal> Menus { get; } =
    [
        new("Archivo", [new("Nuevo"), new("Abrir carpeta"), new("Cerrar carpeta"), ItemMenu.Separador,
            new("Guardar"), new("Guardar como"), new("Guardar todo"), ItemMenu.Separador, new("Salir")]),
        new("Editar", [new("Deshacer"), new("Rehacer"), ItemMenu.Separador,
            new("Cortar"), new("Copiar"), new("Pegar"), ItemMenu.Separador,
            new("Buscar"), new("Buscar y reemplazar"), new("Ir a línea")]),
        new("Ver", [new("Explorador"), new("Terminal"), new("Panel derecho")]),
        new("Ejecutar", [new("Construir"), new("Pausar / Reanudar"), ItemMenu.Separador, new("Ver salida")]),
        new("Ayuda", [new("Acerca de"), new("Atajos de teclado"), new("Sintaxis de GGEZ")]),
    ];

    // conectado por MainWindowViewModel; recibe el texto exacto del item elegido (ej. "Guardar")
    public Action<string>? OnOpcionElegida { get; set; }

    // maneja el "click afuera cierra" a mano (ver MainWindow.axaml): los Popup con
    // IsLightDismissEnabled ponen una capa invisible sobre toda la ventana que tapaba el hover
    // hacia los otros encabezados mientras uno estaba abierto
    public bool HayMenuAbierto => Menus.Any(m => m.EsAbierto);

    public MenuBarViewModel()
    {
        foreach (var menu in Menus)
            menu.PropertyChanged += (_, e) =>
            {
                if (e.PropertyName == nameof(MenuPrincipal.EsAbierto)) OnPropertyChanged(nameof(HayMenuAbierto));
            };
    }

    [RelayCommand]
    void AbrirOCerrar(MenuPrincipal menu)
    {
        bool yaAbierto = menu.EsAbierto;
        CerrarTodos();
        menu.EsAbierto = !yaAbierto;
    }

    // con un menu ya abierto, pasar el mouse sobre otro encabezado lo abre a ese (igual que el original)
    public void AbrirSiOtroYaEstaAbierto(MenuPrincipal menu)
    {
        if (menu.EsAbierto || !Menus.Any(m => m.EsAbierto)) return;
        CerrarTodos();
        menu.EsAbierto = true;
    }

    public void CerrarTodos()
    {
        foreach (var menu in Menus) menu.EsAbierto = false;
    }

    [RelayCommand]
    void Elegir(string opcion)
    {
        CerrarTodos();
        if (opcion != Separador) OnOpcionElegida?.Invoke(opcion);
    }
}
