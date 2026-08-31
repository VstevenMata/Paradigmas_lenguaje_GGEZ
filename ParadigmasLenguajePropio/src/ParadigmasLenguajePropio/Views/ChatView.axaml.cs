using Avalonia.Controls;
using Avalonia.Input;
using ParadigmasLenguajePropio.ViewModels;

namespace ParadigmasLenguajePropio.Views;

public partial class ChatView : UserControl
{
    ChatViewModel? ViewModel => DataContext as ChatViewModel;

    public ChatView()
    {
        InitializeComponent();

        var entrada = this.FindControl<TextBox>("PART_Entrada")!;
        entrada.KeyDown += Entrada_KeyDown;
    }

    // Enter envia, Shift+Enter mete un salto de linea (AcceptsReturn en el TextBox), igual que
    // el input de chat de VS Code
    void Entrada_KeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter || e.KeyModifiers.HasFlag(KeyModifiers.Shift)) return;
        if (ViewModel is not { } vm) return;

        e.Handled = true;
        if (vm.EnviarCommand.CanExecute(null)) vm.EnviarCommand.Execute(null);
    }
}
