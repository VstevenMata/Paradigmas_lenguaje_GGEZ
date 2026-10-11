using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace ParadigmasLenguajePropio.Services;

// Reemplaza los selectores de zenity (Linux) / PowerShell+WinForms (Windows) de Ggez.cs
// por IStorageProvider, nativo y multiplataforma.
public class FileDialogService
{
    readonly Window _window;

    public FileDialogService(Window window)
    {
        _window = window;
    }

    public async Task<string?> ElegirCarpetaAsync(string titulo = "Elegir una carpeta")
    {
        var resultado = await _window.StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = titulo,
            AllowMultiple = false,
        });

        return resultado.Count > 0 ? resultado[0].TryGetLocalPath() : null;
    }

    public async Task<string?> GuardarComoAsync(string nombreSugerido, string titulo = "Guardar como")
    {
        var resultado = await _window.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = titulo,
            SuggestedFileName = nombreSugerido,
        });

        return resultado?.TryGetLocalPath();
    }
}
