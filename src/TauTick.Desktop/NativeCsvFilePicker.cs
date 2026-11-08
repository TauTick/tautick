using Avalonia.Controls;
using Avalonia.Platform.Storage;

namespace TauTick.Desktop;

public sealed class NativeCsvFilePicker(Window owner) : ICsvFilePicker
{
    public async Task<CsvFile?> PickAsync()
    {
        if (!owner.StorageProvider.CanOpen)
            throw new NotSupportedException("File selection is unavailable on this desktop.");
        var files = await owner.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open a local CSV", AllowMultiple = false,
            FileTypeFilter = [new FilePickerFileType("CSV files") { Patterns = ["*.csv"], MimeTypes = ["text/csv"] }]
        });
        if (files.Count == 0) return null;
        var file = files[0];
        foreach (var extra in files.Skip(1)) extra.Dispose();
        return new CsvFile(file.Name, file.OpenReadAsync, file.Dispose, file.TryGetLocalPath());
    }
}
