using Paintty.Imaging;

namespace Paintty.Tests;

public class ImagingTests
{
    [Test]
    public void FloodFill_FillsOnlyTheConnectedRegion()
    {
        using var bitmap = Bitmaps.Create(7, 5, SKColors.White);
        // A wall down column 3 splits the image in two.
        for (int y = 0; y < 5; y++) bitmap.SetPixel(3, y, SKColors.Black);

        var changed = FloodFill.Fill(bitmap, 0, 0, SKColors.Red);

        Assert.That(Pixels.Map(bitmap, SKColors.Red), Is.EqualTo(
            "###....\n" +
            "###....\n" +
            "###....\n" +
            "###....\n" +
            "###...."));
        Assert.That(changed, Is.EqualTo(new SKRectI(0, 0, 3, 5)));
    }

    [Test]
    public void FloodFill_FindsItsWayAroundCorners()
    {
        using var bitmap = Bitmaps.Create(5, 5, SKColors.White);
        // An S-shaped corridor: the fill has to run down, up and down again to reach everything.
        foreach (var (x, y) in new[] { (1, 0), (1, 1), (1, 2), (1, 3), (3, 1), (3, 2), (3, 3), (3, 4) })
            bitmap.SetPixel(x, y, SKColors.Black);

        FloodFill.Fill(bitmap, 0, 0, SKColors.Red);

        Assert.That(Pixels.Count(bitmap, SKColors.White), Is.EqualTo(0));
    }

    [Test]
    public void FloodFill_WithTolerance_SpreadsAcrossNearColours()
    {
        using var bitmap = Bitmaps.Create(4, 1, SKColors.White);
        bitmap.SetPixel(1, 0, new SKColor(250, 250, 250));
        bitmap.SetPixel(2, 0, new SKColor(100, 100, 100));

        FloodFill.Fill(bitmap, 0, 0, SKColors.Red, tolerance: 10);

        Assert.That(Pixels.Map(bitmap, SKColors.Red), Is.EqualTo("##.."));
    }

    [Test]
    public void FloodFill_SameColour_ChangesNothing()
    {
        using var bitmap = Bitmaps.Create(3, 3, SKColors.White);
        Assert.That(FloodFill.Fill(bitmap, 1, 1, SKColors.White).IsEmpty, Is.True);
        Assert.That(FloodFill.Fill(bitmap, 5, 5, SKColors.Red).IsEmpty, Is.True, "outside the image");
    }

    [Test]
    public void Invert_FlipsEveryChannel()
    {
        using var bitmap = Bitmaps.Create(2, 1, new SKColor(10, 200, 0));
        using var inverted = Adjustments.Invert(bitmap);
        Assert.That(inverted.GetPixel(0, 0), Is.EqualTo(new SKColor(245, 55, 255)));
    }

    [Test]
    public void Grayscale_LeavesEqualChannels()
    {
        using var bitmap = Bitmaps.Create(1, 1, new SKColor(200, 30, 90));
        using var gray = Adjustments.Grayscale(bitmap);
        var pixel = gray.GetPixel(0, 0);
        Assert.That(pixel.Red, Is.EqualTo(pixel.Green).Within(1));
        Assert.That(pixel.Green, Is.EqualTo(pixel.Blue).Within(1));
    }

    [Test]
    public void Rotate90_SwapsDimensionsAndMovesPixelsClockwise()
    {
        using var bitmap = Bitmaps.Create(3, 2, SKColors.White);
        bitmap.SetPixel(0, 0, SKColors.Black); // top-left goes to top-right

        using var rotated = Adjustments.Rotate(bitmap, 90);

        Assert.That((rotated.Width, rotated.Height), Is.EqualTo((2, 3)));
        Assert.That(Pixels.Map(rotated, SKColors.Black), Is.EqualTo(".#\n..\n.."));
    }

    [Test]
    public void Rotate_FourQuarterTurns_IsTheIdentity()
    {
        using var bitmap = Bitmaps.Create(5, 3, SKColors.White);
        bitmap.SetPixel(1, 2, SKColors.Black);
        bitmap.SetPixel(4, 0, SKColors.Red);

        var turned = bitmap;
        for (int i = 0; i < 4; i++)
        {
            var next = Adjustments.Rotate(turned, 90);
            if (turned != bitmap) turned.Dispose();
            turned = next;
        }

        Assert.That(Bitmaps.PixelsEqual(turned, bitmap), Is.True);
        turned.Dispose();
    }

    [Test]
    public void Mirror_Horizontal_ReversesRows()
    {
        using var bitmap = Bitmaps.Create(3, 1, SKColors.White);
        bitmap.SetPixel(0, 0, SKColors.Black);
        using var mirrored = Adjustments.Mirror(bitmap, Flip.Horizontal);
        Assert.That(Pixels.Map(mirrored, SKColors.Black), Is.EqualTo("..#"));
    }

    [Test]
    public void Mirror_Vertical_ReversesColumns()
    {
        using var bitmap = Bitmaps.Create(1, 3, SKColors.White);
        bitmap.SetPixel(0, 0, SKColors.Black);
        using var mirrored = Adjustments.Mirror(bitmap, Flip.Vertical);
        Assert.That(Pixels.Map(mirrored, SKColors.Black), Is.EqualTo(".\n.\n#"));
    }

    [Test]
    public void Resize_Nearest_KeepsHardEdges()
    {
        using var bitmap = Bitmaps.Create(2, 1, SKColors.White);
        bitmap.SetPixel(0, 0, SKColors.Black);

        using var resized = Adjustments.Resize(bitmap, 4, 2, smooth: false);

        Assert.That(Pixels.Map(resized, SKColors.Black), Is.EqualTo("##..\n##.."));
    }

    [TestCase(Anchor.TopLeft, 0, 0)]
    [TestCase(Anchor.Center, 1, 2)]
    [TestCase(Anchor.BottomRight, 2, 4)]
    public void CanvasSize_PlacesTheImageByAnchor(Anchor anchor, int x, int y)
    {
        using var bitmap = Bitmaps.Create(2, 2, SKColors.Black);
        using var bigger = Adjustments.CanvasSize(bitmap, 4, 6, anchor, SKColors.White);

        Assert.That((bigger.Width, bigger.Height), Is.EqualTo((4, 6)));
        Assert.That(bigger.GetPixel(x, y), Is.EqualTo(SKColors.Black));
        Assert.That(Pixels.Count(bigger, SKColors.Black), Is.EqualTo(4));
    }

    [Test]
    public void Copy_TakesTheRegion()
    {
        using var bitmap = Bitmaps.Create(4, 4, SKColors.White);
        bitmap.SetPixel(2, 1, SKColors.Black);
        using var part = bitmap.Copy(new SKRectI(2, 1, 4, 3));
        Assert.That(Pixels.Map(part, SKColors.Black), Is.EqualTo("#.\n.."));
    }

    [TestCase(".png")]
    [TestCase(".webp")]
    public async Task Codec_RoundTripsLosslessFormats(string extension)
    {
        var codec = new ImageCodec();
        string path = Path.Combine(Path.GetTempPath(), $"paintty-{Guid.NewGuid():N}{extension}");
        using var original = Bitmaps.Create(6, 4, SKColors.White);
        original.SetPixel(1, 2, SKColors.Red);
        original.SetPixel(5, 3, new SKColor(0, 0, 255, 128));

        try
        {
            await codec.SaveAsync(original, path);
            using var loaded = await codec.LoadAsync(path);

            Assert.That((loaded.Width, loaded.Height), Is.EqualTo((6, 4)));
            Assert.That(loaded.GetPixel(1, 2), Is.EqualTo(SKColors.Red));
            Assert.That(loaded.GetPixel(5, 3).Alpha, Is.EqualTo(128).Within(1));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Codec_SavesJpeg()
    {
        var codec = new ImageCodec();
        string path = Path.Combine(Path.GetTempPath(), $"paintty-{Guid.NewGuid():N}.jpg");
        using var original = Bitmaps.Create(16, 16, SKColors.Red);
        try
        {
            await codec.SaveAsync(original, path);
            using var loaded = await codec.LoadAsync(path);
            Assert.That(loaded.GetPixel(8, 8).Red, Is.GreaterThan(240));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public void Codec_RefusesUnknownExtensions()
    {
        using var bitmap = Bitmaps.Create(1, 1, SKColors.White);
        Assert.ThrowsAsync<NotSupportedException>(() => new ImageCodec().SaveAsync(bitmap, "picture.tga"));
    }

    [Test]
    public void Codec_ReportsFilesThatAreNotImages()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, "not an image");
        try
        {
            Assert.ThrowsAsync<InvalidDataException>(() => new ImageCodec().LoadAsync(path));
        }
        finally
        {
            File.Delete(path);
        }
    }
}
