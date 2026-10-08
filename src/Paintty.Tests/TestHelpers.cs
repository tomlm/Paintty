using Paintty.Editing;
using Paintty.Imaging;
using Paintty.Services;
using Paintty.Tools;

namespace Paintty.Tests;

internal static class Pixels
{
    /// <summary>Renders a bitmap as rows of characters — '#' for <paramref name="ink" />, '.' otherwise.</summary>
    public static string Map(SKBitmap bitmap, SKColor ink)
    {
        var rows = new List<string>();
        for (int y = 0; y < bitmap.Height; y++)
            rows.Add(new string([.. Enumerable.Range(0, bitmap.Width).Select(x => bitmap.GetPixel(x, y) == ink ? '#' : '.')]));
        return string.Join("\n", rows);
    }

    public static int Count(SKBitmap bitmap, SKColor color) =>
        Enumerable.Range(0, bitmap.Height).Sum(y => Enumerable.Range(0, bitmap.Width).Count(x => bitmap.GetPixel(x, y) == color));

    public static Document Blank(int width = 10, int height = 10) => Document.CreateBlank(width, height, SKColors.White);
}

internal sealed class TestHost(Document document) : IToolHost
{
    public Document Document { get; } = document;
    public SKColor Primary { get; set; } = SKColors.Black;
    public SKColor Secondary { get; set; } = SKColors.White;
    public int OverlayInvalidations { get; private set; }
    public string? TextToType { get; set; }

    public void InvalidateOverlay() => OverlayInvalidations++;

    public Task<string?> PromptTextAsync(string title) => Task.FromResult(TextToType);

    public static ToolPointer At(float x, float y, PointerButton button = PointerButton.Primary, bool shift = false) =>
        new(new SKPoint(x + 0.5f, y + 0.5f), button, shift);
}

/// <summary>Answers every dialog with whatever the test has queued.</summary>
internal sealed class FakeDialogs : IDialogService
{
    public SaveChoice SaveChoice { get; set; } = SaveChoice.Cancel;
    public ImageSize? NewSize { get; set; }
    public ResizeRequest? Resize { get; set; }
    public CanvasSizeRequest? CanvasSize { get; set; }
    public SKColor? Color { get; set; }
    public string? Text { get; set; }
    public List<string> Errors { get; } = [];
    public int SaveQuestions { get; private set; }

    public Task<SaveChoice> AskSaveChangesAsync(string documentName)
    {
        SaveQuestions++;
        return Task.FromResult(SaveChoice);
    }

    public Task ShowErrorAsync(string title, string message)
    {
        Errors.Add(message);
        return Task.CompletedTask;
    }

    public List<string> Messages { get; } = [];

    public Task ShowMessageAsync(string title, string message)
    {
        Messages.Add(message);
        return Task.CompletedTask;
    }

    public Task<ImageSize?> AskNewImageSizeAsync(ImageSize suggested) => Task.FromResult(NewSize);
    public Task<ResizeRequest?> AskResizeAsync(ImageSize current) => Task.FromResult(Resize);
    public Task<CanvasSizeRequest?> AskCanvasSizeAsync(ImageSize current) => Task.FromResult(CanvasSize);
    public Task<SKColor?> EditColorAsync(SKColor color) => Task.FromResult(Color);
    public Task<string?> PromptTextAsync(string title) => Task.FromResult(Text);
}

internal sealed class FakeFiles : IFileDialogService
{
    public string? OpenPath { get; set; }
    public string? SavePath { get; set; }

    public Task<string?> PickImageToOpenAsync() => Task.FromResult(OpenPath);
    public Task<string?> PickImageToSaveAsync(string? currentPath) => Task.FromResult(SavePath);
}

internal sealed class QuadMetrics : IDisplayMetrics
{
    public double PixelsPerCellX => 2;
    public double PixelsPerCellY => 4;
    public string Graphics => "quadrant blocks";
}
