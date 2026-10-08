using SkiaSharp;

namespace Paintty.Imaging;

/// <summary>
///     The handful of whole-bitmap operations every other part of the editor builds on. Every bitmap
///     the editor owns is BGRA, premultiplied, so it can be drawn into with an <see cref="SKCanvas" />
///     and handed to Avalonia without conversion.
/// </summary>
public static class Bitmaps
{
    public static SKImageInfo Info(int width, int height) =>
        new(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);

    public static SKBitmap Create(int width, int height, SKColor fill)
    {
        var bitmap = new SKBitmap(Info(width, height));
        bitmap.Erase(fill);
        return bitmap;
    }

    /// <summary>Converts whatever a decoder produced into the editor's pixel format.</summary>
    public static SKBitmap Normalize(SKBitmap source)
    {
        if (source.ColorType == SKColorType.Bgra8888 && source.AlphaType == SKAlphaType.Premul)
            return source;

        var converted = new SKBitmap(Info(source.Width, source.Height));
        using (var canvas = new SKCanvas(converted))
        {
            canvas.Clear(SKColors.Transparent);
            canvas.DrawBitmap(source, 0, 0);
        }

        source.Dispose();
        return converted;
    }

    public static SKRectI Bounds(this SKBitmap bitmap) => new(0, 0, bitmap.Width, bitmap.Height);

    /// <summary>The part of <paramref name="rect" /> that lies on the bitmap; empty when none does.</summary>
    public static SKRectI Clip(this SKBitmap bitmap, SKRectI rect)
    {
        var clipped = SKRectI.Intersect(rect, bitmap.Bounds());
        return clipped.IsEmpty ? SKRectI.Empty : clipped;
    }

    /// <summary>Copies a region out into a bitmap of its own.</summary>
    public static SKBitmap Copy(this SKBitmap source, SKRectI region)
    {
        var copy = new SKBitmap(Info(region.Width, region.Height));
        using var canvas = new SKCanvas(copy);
        canvas.Clear(SKColors.Transparent);
        canvas.DrawBitmap(source, region, new SKRect(0, 0, region.Width, region.Height), CopyPaint);
        return copy;
    }

    /// <summary>Writes <paramref name="source" /> over the target, replacing pixels rather than blending.</summary>
    public static void Replace(this SKBitmap target, SKBitmap source, int x, int y)
    {
        using var canvas = new SKCanvas(target);
        canvas.DrawBitmap(source, x, y, CopyPaint);
    }

    /// <summary>Draws <paramref name="source" /> over the target, blending by alpha.</summary>
    public static void Composite(this SKBitmap target, SKBitmap source, int x, int y)
    {
        using var canvas = new SKCanvas(target);
        canvas.DrawBitmap(source, x, y);
    }

    public static void FillRect(this SKBitmap target, SKRectI rect, SKColor color)
    {
        using var canvas = new SKCanvas(target);
        using var paint = new SKPaint { Color = color, BlendMode = SKBlendMode.Src };
        canvas.DrawRect(rect, paint);
    }

    /// <summary>The colour at a pixel, un-premultiplied.</summary>
    public static SKColor ColorAt(this SKBitmap bitmap, int x, int y) =>
        x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height ? SKColors.Transparent : bitmap.GetPixel(x, y);

    public static bool PixelsEqual(SKBitmap a, SKBitmap b) =>
        a.Width == b.Width && a.Height == b.Height && a.GetPixelSpan().SequenceEqual(b.GetPixelSpan());

    private static readonly SKPaint CopyPaint = new() { BlendMode = SKBlendMode.Src };
}
