using Paintty.Imaging;
using SkiaSharp;

namespace Paintty.Services;

public enum SaveChoice { Save, Discard, Cancel }

public sealed record ImageSize(int Width, int Height);

public sealed record ResizeRequest(int Width, int Height, bool Smooth);

public sealed record CanvasSizeRequest(int Width, int Height, Anchor Anchor);

/// <summary>Everything the editor asks the user, so view models never touch a window.</summary>
public interface IDialogService
{
    Task<SaveChoice> AskSaveChangesAsync(string documentName);
    Task ShowErrorAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
    Task<ImageSize?> AskNewImageSizeAsync(ImageSize suggested);
    Task<ResizeRequest?> AskResizeAsync(ImageSize current);
    Task<CanvasSizeRequest?> AskCanvasSizeAsync(ImageSize current);
    Task<SKColor?> EditColorAsync(SKColor color);
    Task<string?> PromptTextAsync(string title);
}

public interface IFileDialogService
{
    /// <summary>Returns the chosen path, or null when the user cancels.</summary>
    Task<string?> PickImageToOpenAsync();

    /// <summary>Returns the chosen path, or null when the user cancels.</summary>
    Task<string?> PickImageToSaveAsync(string? currentPath);
}
