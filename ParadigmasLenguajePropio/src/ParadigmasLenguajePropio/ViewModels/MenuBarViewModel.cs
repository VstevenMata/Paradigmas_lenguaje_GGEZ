using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace ParadigmasLenguajePropio.ViewModels;

// Menu propio con Popup: el Menu nativo no deja sobreescribir el hover de forma confiable
public partial class MenuBarViewModel : ViewModelBase
{
    const string Separador = "-";

    public IReadOnlyList<MenuPrincipal> Menus { get; } =
    [
        new("Archivo", [new("Nuevo", "Ctrl+N"), new("Abrir carpeta", "Ctrl+O"), new("Cerrar carpeta"), ItemMenu.Separador,
            new("Guardar", "Ctrl+S"), new("Guardar como", "Ctrl+Shift+S"), new("Guardar todo", "Ctrl+Alt+S"), ItemMenu.Separador, new("Salir")]),
        new("Editar", [new("Deshacer", "Ctrl+Z"), new("Rehacer", "Ctrl+Y"), ItemMenu.Separador,
            new("Cortar", "Ctrl+X"), new("Copiar", "Ctrl+C"), new("Pegar", "Ctrl+V"), ItemMenu.Separador,
            new("Buscar", "Ctrl+F"), new("Buscar y reemplazar", "Ctrl+H"), new("Ir a línea", "Ctrl+G")]),
        new("Ver", [new("Explorador", "Ctrl+B"), new("Terminal", "Ctrl+J"), new("Panel derecho"), new("Ajustes"), ItemMenu.Separador,
            new("Acercar", "Ctrl++", "Ctrl+OemPlus"), new("Alejar", "Ctrl+-", "Ctrl+OemMinus"), new("Restablecer zoom", "Ctrl+0", "Ctrl+D0"), ItemMenu.Separador,
            new("Tema: Win95"), new("Tema: Zelda"), new("Tema: Sonic"), new("Tema: Mega Man")]),
        new("Ejecutar", [new("Construir", "F5"), new("Pausar / Reanudar"), ItemMenu.Separador, new("Ver salida")]),
        new("Ayuda", [new("Acerca de"), new("Atajos de teclado", "F1"), new("Sintaxis de GGEZ")]),
    ];

    // Atajos globales: solo llegan aca las teclas que el control enfocado no marco como manejadas
    public bool EjecutarAtajo(KeyEventArgs e)
    {
        var item = Menus.SelectMany(m => m.Items).FirstOrDefault(i => i.Gesto?.Matches(e) == true);
        if (item == null) return false;
        Elegir(item.Texto);
        return true;
    }

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
