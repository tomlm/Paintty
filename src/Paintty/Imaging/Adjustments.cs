using SkiaSharp;

namespace Paintty.Imaging;

public enum Anchor
{
    TopLeft, Top, TopRight,
    Left, Center, Right,
    BottomLeft, Bottom, BottomRight
}

public enum Flip { Horizontal, Vertical }

/// <summary>
///     Whole-bitmap transforms. Each returns a new bitmap and leaves its source alone, so the caller
///     can keep the original for undo.
/// </summary>
public static class Adjustments
{
    public static SKBitmap Invert(SKBitmap source) => Filtered(source, SKColorFilter.CreateColorMatrix(
    [
        -1, 0, 0, 0, 1,
        0, -1, 0, 0, 1,
        0, 0, -1, 0, 1,
        0, 0, 0, 1, 0
    ]));

    /// <summary>Rec. 709 luma, the weighting Paint.NET's Black and White uses.</summary>
    public static SKBitmap Grayscale(SKBitmap source) => Filtered(source, SKColorFilter.CreateColorMatrix(
    [
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0.2126f, 0.7152f, 0.0722f, 0, 0,
        0, 0, 0, 1, 0
    ]));

    public static SKBitmap Mirror(SKBitmap source, Flip flip) =>
        Transformed(source, source.Width, source.Height, canvas =>
        {
            if (flip == Flip.Horizontal)
                canvas.Scale(-1, 1, source.Width / 2f, 0);
            else
                canvas.Scale(1, -1, 0, source.Height / 2f);
        });

    /// <summary>Rotates clockwise by a multiple of 90°.</summary>
    public static SKBitmap Rotate(SKBitmap source, int degrees)
    {
        degrees = ((degrees % 360) + 360) % 360;
        if (degrees % 90 != 0)
            throw new ArgumentOutOfRangeException(nameof(degrees), "Only right-angle rotations are supported.");

        bool swap = degrees is 90 or 270;
        int width = swap ? source.Height : source.Width;
        int height = swap ? source.Width : source.Height;
        return Transformed(source, width, height, canvas =>
        {
            canvas.Translate(width / 2f, height / 2f);
            canvas.RotateDegrees(degrees);
            canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        });
    }

    /// <summary>
    ///     Rotates clockwise by any angle. Right angles are exact (see <see cref="Rotate" />); any other
    ///     angle is resampled smoothly, and the corners it uncovers are transparent.
    /// </summary>
    /// <param name="expand">
    ///     Grow the canvas to the rotated image's bounding box, so nothing is cut off; otherwise keep
    ///     the size and turn the image about its centre, cropping the corners.
    /// </param>
    public static SKBitmap RotateFree(SKBitmap source, double degrees, bool expand)
    {
        degrees = ((degrees % 360) + 360) % 360;
        if (degrees % 90 == 0 && (expand || degrees % 180 == 0 || source.Width == source.Height))
            return Rotate(source, (int)degrees);

        int width = source.Width, height = source.Height;
        if (expand)
        {
            double radians = degrees * Math.PI / 180;
            double cos = Math.Abs(Math.Cos(radians)), sin = Math.Abs(Math.Sin(radians));
            // a hair under, so a rounding error doesn't add a transparent row
            width = Math.Max(1, (int)Math.Ceiling(source.Width * cos + source.Height * sin - 1e-6));
            height = Math.Max(1, (int)Math.Ceiling(source.Width * sin + source.Height * cos - 1e-6));
        }

        using var image = SKImage.FromBitmap(source);
        using var paint = new SKPaint { IsAntialias = true };
        var result = new SKBitmap(Bitmaps.Info(width, height));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        canvas.Translate(width / 2f, height / 2f);
        canvas.RotateDegrees((float)degrees);
        canvas.Translate(-source.Width / 2f, -source.Height / 2f);
        canvas.DrawImage(image, 0, 0, new SKSamplingOptions(SKFilterMode.Linear, SKMipmapMode.None), paint);
        return result;
    }

    /// <summary>Scales the image to a new size; <paramref name="smooth" /> off keeps hard pixel edges.</summary>
    public static SKBitmap Resize(SKBitmap source, int width, int height, bool smooth = true)
    {
        var sampling = smooth
            ? new SKSamplingOptions(SKCubicResampler.Mitchell)
            : new SKSamplingOptions(SKFilterMode.Nearest, SKMipmapMode.None);
        return source.Resize(Bitmaps.Info(width, height), sampling)
               ?? throw new InvalidOperationException($"Skia could not resize to {width}×{height}.");
    }

    /// <summary>Changes the canvas size without scaling, placing the old image by <paramref name="anchor" />.</summary>
    public static SKBitmap CanvasSize(SKBitmap source, int width, int height, Anchor anchor, SKColor background)
    {
        var (x, y) = Offset(source.Width, source.Height, width, height, anchor);
        var result = Bitmaps.Create(width, height, background);
        result.Replace(source, x, y);
        return result;
    }

    public static (int X, int Y) Offset(int oldWidth, int oldHeight, int newWidth, int newHeight, Anchor anchor)
    {
        int column = (int)anchor % 3, row = (int)anchor / 3;
        return (column * (newWidth - oldWidth) / 2, row * (newHeight - oldHeight) / 2);
    }

    private static SKBitmap Filtered(SKBitmap source, SKColorFilter filter)
    {
        using (filter)
        using (var paint = new SKPaint { ColorFilter = filter, BlendMode = SKBlendMode.Src })
            return Transformed(source, source.Width, source.Height, _ => { }, paint);
    }

    private static SKBitmap Transformed(SKBitmap source, int width, int height, Action<SKCanvas> transform,
        SKPaint? paint = null)
    {
        var result = new SKBitmap(Bitmaps.Info(width, height));
        using var canvas = new SKCanvas(result);
        canvas.Clear(SKColors.Transparent);
        transform(canvas);
        canvas.DrawBitmap(source, 0, 0, paint);
        return result;
    }
}
