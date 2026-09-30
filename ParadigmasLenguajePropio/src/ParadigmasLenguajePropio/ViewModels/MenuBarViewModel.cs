using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ParadigmasLenguajePropio.ViewModels;

// Menu propio con Popup: el Menu nativo no deja sobreescribir el hover de forma confiable
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
        new("Ver", [new("Explorador"), new("Terminal"), new("Panel derecho"), new("Ajustes"), ItemMenu.Separador,
            new("Acercar"), new("Alejar"), new("Restablecer zoom"), ItemMenu.Separador,
            new("Tema: Win95"), new("Tema: Zelda"), new("Tema: Sonic"), new("Tema: Mega Man")]),
        new("Ejecutar", [new("Construir"), new("Pausar / Reanudar"), ItemMenu.Separador, new("Ver salida")]),
        new("Ayuda", [new("Acerca de"), new("Atajos de teclado"), new("Sintaxis de GGEZ")]),
    ];

    public Action<string>? OnOpcionElegida { get; set; }

    // Click afuera manejado a mano: IsLightDismissEnabled tapaba el hover de los otros encabezados
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
