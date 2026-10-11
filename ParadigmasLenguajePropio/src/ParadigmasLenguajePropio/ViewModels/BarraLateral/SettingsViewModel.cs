using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using ParadigmasLenguajePropio.Models;
using ParadigmasLenguajePropio.Services;

namespace ParadigmasLenguajePropio.ViewModels;

// Panel de Ajustes sin estado propio: delega en EditorAreaViewModel y ExtensionsViewModel
public partial class SettingsViewModel : ViewModelBase
{
    readonly EditorAreaViewModel _editor;
    public ExtensionsViewModel Extensiones { get; }

    public double TamanoFuente => _editor.TamanoFuente;
    public int TamanoTab => _editor.TamanoTab;

    public ObservableCollection<OpcionTema> Temas { get; } = new();

    public SettingsViewModel(EditorAreaViewModel editor, ExtensionsViewModel extensiones)
    {
        _editor = editor;
        Extensiones = extensiones;

        _editor.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(EditorAreaViewModel.TamanoFuente)) OnPropertyChanged(nameof(TamanoFuente));
            else if (e.PropertyName == nameof(EditorAreaViewModel.TamanoTab)) OnPropertyChanged(nameof(TamanoTab));
        };

        Extensiones.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ExtensionsViewModel.TemasInstalados)) SincronizarTemas();
        };
        SincronizarTemas();
    }

    void SincronizarTemas()
    {
        Temas.Clear();
        Temas.Add(new OpcionTema("Win95", Tema.Win95));
        foreach (var opcion in Extensiones.TemasInstalados) Temas.Add(opcion);
    }

    [RelayCommand] void Acercar() => _editor.CambiarZoom(1);
    [RelayCommand] void Alejar() => _editor.CambiarZoom(-1);
    [RelayCommand] void RestablecerZoom() => _editor.CambiarZoom(0);

    [RelayCommand] void AumentarTab() => _editor.CambiarTamanoTab(1);
    [RelayCommand] void DisminuirTab() => _editor.CambiarTamanoTab(-1);

    [RelayCommand]
    void ElegirTema(OpcionTema opcion) => Extensiones.SeleccionarTema(opcion.Valor);
}
