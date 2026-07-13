using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace testdaemon.Service;

public static class DialogService
{
    public static Window? MainWindow { get; set; }

    public static async Task<string?> PickFolderAsync(string title)
    {
        if (MainWindow?.StorageProvider is not { } provider) return null;

        var folders = await provider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = title,
            AllowMultiple = false,
        });

        return folders.Count > 0 ? folders[0].Path.LocalPath : null;
    }
}