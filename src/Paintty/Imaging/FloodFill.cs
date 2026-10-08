using SkiaSharp;

namespace Paintty.Imaging;

/// <summary>
///     Scanline flood fill. Skia has nothing like it, so this is the one place the editor reads and
///     writes pixels directly; everything else goes through an <see cref="SKCanvas" />.
/// </summary>
public static class FloodFill
{
    /// <summary>
    ///     Fills the region connected to (<paramref name="x" />, <paramref name="y" />) whose colours are
    ///     within <paramref name="tolerance" /> (0–255, per channel) of the seed colour.
    /// </summary>
    /// <returns>The bounds of the pixels that changed; empty when nothing did.</returns>
    public static SKRectI Fill(SKBitmap bitmap, int x, int y, SKColor color, int tolerance = 0)
    {
        if (x < 0 || y < 0 || x >= bitmap.Width || y >= bitmap.Height)
            return SKRectI.Empty;

        int width = bitmap.Width, height = bitmap.Height;
        Span<uint> pixels = System.Runtime.InteropServices.MemoryMarshal.Cast<byte, uint>(bitmap.GetPixelSpan());
        uint seed = pixels[y * width + x];
        uint fill = Premultiplied(color);
        if (seed == fill)
            return SKRectI.Empty;

        var visited = new bool[width * height];
        var stack = new Stack<(int X, int Y)>();
        stack.Push((x, y));
        int minX = x, maxX = x, minY = y, maxY = y;

        while (stack.Count > 0)
        {
            var (sx, sy) = stack.Pop();
            int row = sy * width;
            if (visited[row + sx] || !Matches(pixels[row + sx], seed, tolerance))
                continue;

            // Walk out to both ends of this run, then queue the rows above and below it.
            int left = sx, right = sx;
            while (left > 0 && !visited[row + left - 1] && Matches(pixels[row + left - 1], seed, tolerance)) left--;
            while (right < width - 1 && !visited[row + right + 1] && Matches(pixels[row + right + 1], seed, tolerance)) right++;

            for (int i = left; i <= right; i++)
            {
                pixels[row + i] = fill;
                visited[row + i] = true;
            }

            minX = Math.Min(minX, left);
            maxX = Math.Max(maxX, right);
            minY = Math.Min(minY, sy);
            maxY = Math.Max(maxY, sy);

            if (sy > 0) QueueRuns(pixels, visited, stack, width, left, right, sy - 1, seed, tolerance);
            if (sy < height - 1) QueueRuns(pixels, visited, stack, width, left, right, sy + 1, seed, tolerance);
        }

        bitmap.NotifyPixelsChanged();
        return new SKRectI(minX, minY, maxX + 1, maxY + 1);
    }

    /// <summary>Packs a colour as a premultiplied BGRA pixel, the layout <see cref="Bitmaps" /> uses.</summary>
    internal static uint Premultiplied(SKColor color)
    {
        uint a = color.Alpha;
        uint r = color.Red * a / 255, g = color.Green * a / 255, b = color.Blue * a / 255;
        return (a << 24) | (r << 16) | (g << 8) | b;
    }

    /// <summary>Pushes one seed for each run of fillable pixels on row <paramref name="y" /> between left and right.</summary>
    private static void QueueRuns(Span<uint> pixels, bool[] visited, Stack<(int X, int Y)> stack, int width,
        int left, int right, int y, uint seed, int tolerance)
    {
        int row = y * width;
        bool inRun = false;
        for (int i = left; i <= right; i++)
        {
            bool open = !visited[row + i] && Matches(pixels[row + i], seed, tolerance);
            if (open && !inRun) stack.Push((i, y));
            inRun = open;
        }
    }

    private static bool Matches(uint pixel, uint seed, int tolerance)
    {
        if (pixel == seed) return true;
        if (tolerance <= 0) return false;
        for (int shift = 0; shift < 32; shift += 8)
        {
            int delta = (int)((pixel >> shift) & 0xFF) - (int)((seed >> shift) & 0xFF);
            if (Math.Abs(delta) > tolerance) return false;
        }

        return true;
    }
}
