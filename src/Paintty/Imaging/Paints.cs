using SkiaSharp;

namespace Paintty.Imaging;

/// <summary>The <see cref="SKPaint" /> shapes the tools draw with.</summary>
public static class Paints
{
    public static SKPaint Stroke(SKColor color, float width, bool antialias, SKStrokeCap cap = SKStrokeCap.Round) =>
        new()
        {
            Color = color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = Math.Max(1, width),
            StrokeCap = cap,
            StrokeJoin = cap == SKStrokeCap.Round ? SKStrokeJoin.Round : SKStrokeJoin.Miter,
            IsAntialias = antialias,
            // Replace rather than blend, so drawing with a translucent colour (or erasing to
            // transparent) leaves exactly that colour behind, as in Paint.
            BlendMode = SKBlendMode.Src
        };

    public static SKPaint Fill(SKColor color, bool antialias) =>
        new()
        {
            Color = color,
            Style = SKPaintStyle.Fill,
            IsAntialias = antialias,
            BlendMode = SKBlendMode.Src
        };
}
