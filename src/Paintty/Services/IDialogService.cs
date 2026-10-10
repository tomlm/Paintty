using Paintty.Imaging;
using SkiaSharp;

namespace Paintty.Services;

public enum SaveChoice { Save, Discard, Cancel }

public sealed record ImageSize(int Width, int Height);

public sealed record ResizeRequest(int Width, int Height, bool Smooth);

public sealed record CanvasSizeRequest(int Width, int Height, Anchor Anchor);

/// <summary>
///     A clockwise rotation by any angle, growing the canvas (or the selection) to fit when
///     <paramref name="Expand" />.
/// </summary>
public sealed record RotateRequest(double Degrees, bool Expand);

/// <summary>A key or mouse gesture and what it does, for Help → Keyboard.</summary>
public sealed record Shortcut(string Keys, string Action);

public sealed record ShortcutGroup(string Name, IReadOnlyList<Shortcut> Shortcuts);

/// <summary>Everything the editor asks the user, so view models never touch a window.</summary>
public interface IDialogService
{
    Task<SaveChoice> AskSaveChangesAsync(string documentName);
    Task ShowErrorAsync(string title, string message);
    Task ShowMessageAsync(string title, string message);
    Task<ImageSize?> AskNewImageSizeAsync(ImageSize suggested);
    Task<ResizeRequest?> AskResizeAsync(ImageSize current);
    Task<CanvasSizeRequest?> AskCanvasSizeAsync(ImageSize current);

    /// <summary>
    ///     Asks for an angle, calling <paramref name="preview" /> with each setting as the user changes
    ///     it, so the canvas can show it before it is applied. <paramref name="selection" /> says whether
    ///     a selection is turned rather than the whole image.
    /// </summary>
    Task<RotateRequest?> AskRotateAsync(bool selection, Action<RotateRequest> preview);
    Task<SKColor?> EditColorAsync(SKColor color);
    Task ShowShortcutsAsync(IReadOnlyList<ShortcutGroup> groups);
    Task<string?> PromptTextAsync(string title);
}

public interface IFileDialogService
{
    /// <summary>Returns the chosen path, or null when the user cancels.</summary>
    Task<string?> PickImageToOpenAsync();

    /// <summary>Returns the chosen path, or null when the user cancels.</summary>
    Task<string?> PickImageToSaveAsync(string? currentPath);
}
