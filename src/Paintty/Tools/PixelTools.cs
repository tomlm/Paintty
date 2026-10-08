using CommunityToolkit.Mvvm.ComponentModel;
using Paintty.Imaging;
using SkiaSharp;

namespace Paintty.Tools;

public sealed partial class FillTool : ToolBase
{
    public override string Name => "Fill";
    public override string Glyph => "◙";
    public override char Hotkey => 'F';

    /// <summary>How different a neighbour may be and still be filled, 0–100%.</summary>
    [ObservableProperty] private int _tolerance;

    public override void Press(IToolHost host, ToolPointer pointer)
    {
        var document = host.Document;
        using var edit = document.BeginEdit(Name);
        var changed = FloodFill.Fill(document.Bitmap, pointer.Pixel.X, pointer.Pixel.Y,
            ColorFor(host, pointer.Button), Tolerance * 255 / 100);
        edit.Touch(changed);
    }
}

/// <summary>Picks up the colour under the pointer as the primary (or, right button, secondary) colour.</summary>
public sealed class ColorPickerTool : ToolBase
{
    public override string Name => "Color Picker";
    public override string Glyph => "⌖";
    public override char Hotkey => 'K';

    public override void Press(IToolHost host, ToolPointer pointer) => Pick(host, pointer);
    public override void Drag(IToolHost host, ToolPointer pointer) => Pick(host, pointer);

    private static void Pick(IToolHost host, ToolPointer pointer)
    {
        var bitmap = host.Document.Bitmap;
        if (pointer.Pixel.X < 0 || pointer.Pixel.Y < 0 || pointer.Pixel.X >= bitmap.Width || pointer.Pixel.Y >= bitmap.Height)
            return;

        var color = bitmap.ColorAt(pointer.Pixel.X, pointer.Pixel.Y);
        if (pointer.Button == PointerButton.Primary)
            host.Primary = color;
        else
            host.Secondary = color;
    }
}

/// <summary>Click to place text, typed into a prompt, in the primary colour.</summary>
public sealed partial class TextTool : ToolBase
{
    public override string Name => "Text";
    public override string Glyph => "A";
    public override char Hotkey => 'T';

    [ObservableProperty] private string _fontFamily = SKTypeface.Default.FamilyName;
    [ObservableProperty] private int _fontSize = 16;
    [ObservableProperty] private bool _bold;
    [ObservableProperty] private bool _antialias = true;

    public static IReadOnlyList<string> FontFamilies { get; } =
        [.. SKFontManager.Default.FontFamilies.Distinct().Order(StringComparer.OrdinalIgnoreCase)];

    public override async void Press(IToolHost host, ToolPointer pointer)
    {
        // The prompt is a dialog, so this outlives the pointer event; capture what it needs now.
        var color = ColorFor(host, pointer.Button);
        var at = pointer.Pixel;
        string? text = await host.PromptTextAsync("Text");
        if (!string.IsNullOrEmpty(text))
            DrawText(host, text, at, color);
    }

    /// <summary>Draws <paramref name="text" /> with its top-left at <paramref name="at" />, one line per line.</summary>
    public void DrawText(IToolHost host, string text, SKPointI at, SKColor color)
    {
        using var typeface = SKTypeface.FromFamilyName(FontFamily,
            Bold ? SKFontStyle.Bold : SKFontStyle.Normal) ?? SKTypeface.Default;
        using var font = new SKFont(typeface, FontSize) { Edging = Antialias ? SKFontEdging.Antialias : SKFontEdging.Alias };
        using var paint = new SKPaint { Color = color, IsAntialias = Antialias };

        var metrics = font.Metrics;
        float lineHeight = metrics.Descent - metrics.Ascent + metrics.Leading;
        var document = host.Document;

        using var edit = document.BeginEdit(Name);
        using var canvas = new SKCanvas(document.Bitmap);
        float baseline = at.Y - metrics.Ascent;
        foreach (string line in text.Split('\n'))
        {
            canvas.DrawText(line.TrimEnd('\r'), at.X, baseline, SKTextAlign.Left, font, paint);
            float width = font.MeasureText(line);
            edit.Touch(new SKRect(at.X, baseline + metrics.Ascent, at.X + width, baseline + metrics.Descent), 2);
            baseline += lineHeight;
        }
    }
}
