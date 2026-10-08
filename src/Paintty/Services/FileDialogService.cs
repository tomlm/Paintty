using Avalonia.Controls;
using Avalonia.Platform.Storage;
using Paintty.Imaging;

namespace Paintty.Services;

/// <summary>Open and save pickers from the window's storage provider (Consolonia's, in a terminal).</summary>
public sealed class FileDialogService(Func<TopLevel?> topLevel, IImageCodec codec) : IFileDialogService
{
    public async Task<string?> PickImageToOpenAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open Image",
            AllowMultiple = false,
            FileTypeFilter = [Filter("Images", codec.ReadableExtensions), FilePickerFileTypes.All]
        });
        return files.FirstOrDefault()?.TryGetLocalPath();
    }

    public async Task<string?> PickImageToSaveAsync(string? currentPath)
    {
        var options = new FilePickerSaveOptions
        {
            Title = "Save Image As",
            SuggestedFileName = currentPath is null ? "untitled.png" : Path.GetFileName(currentPath),
            DefaultExtension = currentPath is null ? ".png" : Path.GetExtension(currentPath),
            ShowOverwritePrompt = true,
            FileTypeChoices = [.. codec.WritableExtensions
                .Where(extension => extension != ".jpeg")
                .Select(extension => Filter(extension.TrimStart('.').ToUpperInvariant(), [extension]))]
        };
        if (currentPath is not null && Path.GetDirectoryName(currentPath) is { Length: > 0 } directory)
            options.SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(directory);

        var file = await StorageProvider.SaveFilePickerAsync(options);
        return file?.TryGetLocalPath();
    }

    private IStorageProvider StorageProvider =>
        topLevel()?.StorageProvider ?? throw new InvalidOperationException("There is no window to show a file picker in.");

    private static FilePickerFileType Filter(string name, IEnumerable<string> extensions) =>
        new(name) { Patterns = [.. extensions.Select(extension => "*" + extension)] };
}
