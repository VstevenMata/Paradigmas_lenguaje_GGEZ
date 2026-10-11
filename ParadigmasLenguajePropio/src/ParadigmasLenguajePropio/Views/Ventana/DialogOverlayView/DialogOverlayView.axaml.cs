using Avalonia.Controls;
using Avalonia.Input;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.Views;

public partial class DialogOverlayView : UserControl
{
    DialogService? ViewModel => DataContext as DialogService;

    public DialogOverlayView()
    {
        InitializeComponent();

        // Al mostrarse, el foco va al prompt o al overlay para que Escape funcione
        PropertyChanged += (_, e) =>
        {
            if (e.Property != IsVisibleProperty || !IsVisible) return;
            var campo = this.FindControl<TextBox>("PART_CampoPrompt");
            if (campo != null && campo.IsVisible) campo.Focus();
            else Focus();
        };

        var campoPrompt = this.FindControl<TextBox>("PART_CampoPrompt")!;
        campoPrompt.KeyDown += (_, e) =>
        {
            if (ViewModel is not { } vm) return;
            if (e.Key == Key.Enter) { vm.Elegir(0); e.Handled = true; }
        };
    }

    void Overlay_KeyDown(object? sender, KeyEventArgs e)
    {
        if (ViewModel is not { } vm) return;
        if (e.Key == Key.Escape) { vm.Elegir(vm.Botones.Count - 1); e.Handled = true; }
    }
}
