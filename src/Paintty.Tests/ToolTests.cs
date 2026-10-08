using Paintty.Tools;
using static Paintty.Tests.TestHost;

namespace Paintty.Tests;

public class ToolTests
{
    [Test]
    public void Pencil_DrawsAContinuousLineBetweenDragPoints()
    {
        using var document = Pixels.Blank(8, 3);
        var host = new TestHost(document);
        var pencil = new PencilTool();

        pencil.Press(host, At(0, 1));
        pencil.Drag(host, At(7, 1)); // one big jump: the gap must still be filled
        pencil.Release(host, At(7, 1));

        Assert.That(Pixels.Map(document.Bitmap, SKColors.Black), Is.EqualTo(
            "........\n" +
            "########\n" +
            "........"));
        Assert.That(document.History.CanUndo, Is.True);
    }

    [Test]
    public void Pencil_OneStrokeIsOneUndo()
    {
        using var document = Pixels.Blank();
        var host = new TestHost(document);
        var pencil = new PencilTool();

        pencil.Press(host, At(0, 0));
        pencil.Drag(host, At(3, 3));
        pencil.Drag(host, At(6, 0));
        pencil.Release(host, At(6, 0));

        document.Undo();
        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(0));
        Assert.That(document.History.CanUndo, Is.False);
    }

    [Test]
    public void RightButton_DrawsWithTheSecondaryColour()
    {
        using var document = Pixels.Blank(3, 1);
        var host = new TestHost(document) { Secondary = SKColors.Red };
        var pencil = new PencilTool();

        pencil.Press(host, At(1, 0, PointerButton.Secondary));
        pencil.Release(host, At(1, 0, PointerButton.Secondary));

        Assert.That(document.Bitmap.GetPixel(1, 0), Is.EqualTo(SKColors.Red));
    }

    [Test]
    public void Cancel_AbandonsTheStroke()
    {
        using var document = Pixels.Blank();
        var host = new TestHost(document);
        var brush = new BrushTool { Size = 3 };

        brush.Press(host, At(2, 2));
        brush.Drag(host, At(6, 6));
        brush.Cancel(host);

        Assert.That(Pixels.Count(document.Bitmap, SKColors.White), Is.EqualTo(100));
        Assert.That(document.History.CanUndo, Is.False);
    }

    [Test]
    public void Brush_IsAsWideAsItsSize()
    {
        using var document = Pixels.Blank(9, 9);
        var host = new TestHost(document);
        var brush = new BrushTool { Size = 3 };

        brush.Press(host, At(1, 4));
        brush.Drag(host, At(7, 4));
        brush.Release(host, At(7, 4));

        // Down the middle column the stroke covers rows 3–5.
        var column = Enumerable.Range(0, 9).Select(y => document.Bitmap.GetPixel(4, y) == SKColors.Black).ToArray();
        Assert.That(column, Is.EqualTo(new[] { false, false, false, true, true, true, false, false, false }));
    }

    [Test]
    public void Eraser_PaintsTheSecondaryColour()
    {
        using var document = Paintty.Editing.Document.CreateBlank(4, 4, SKColors.Black);
        var host = new TestHost(document) { Secondary = SKColors.White };
        var eraser = new EraserTool { Size = 1 };

        eraser.Press(host, At(0, 0));
        eraser.Drag(host, At(3, 0));
        eraser.Release(host, At(3, 0));

        Assert.That(Pixels.Map(document.Bitmap, SKColors.White), Is.EqualTo("####\n....\n....\n...."));
    }

    [Test]
    public void Eraser_ToTransparent_ClearsAlpha()
    {
        using var document = Paintty.Editing.Document.CreateBlank(2, 1, SKColors.Black);
        var host = new TestHost(document);
        var eraser = new EraserTool { Size = 1, ToTransparent = true };

        eraser.Press(host, At(0, 0));
        eraser.Release(host, At(0, 0));

        Assert.That(document.Bitmap.GetPixel(0, 0).Alpha, Is.EqualTo(0));
    }

    [Test]
    public void Rectangle_PreviewsInTheOverlay_AndDrawsOnRelease()
    {
        using var document = Pixels.Blank(6, 5);
        var host = new TestHost(document);
        var rectangle = new RectangleTool();

        rectangle.Press(host, At(1, 1));
        rectangle.Drag(host, At(4, 3));
        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(0), "only previewed so far");
        Assert.That(host.OverlayInvalidations, Is.GreaterThan(0));

        rectangle.Release(host, At(4, 3));

        Assert.That(Pixels.Map(document.Bitmap, SKColors.Black), Is.EqualTo(
            "......\n" +
            ".####.\n" +
            ".#..#.\n" +
            ".####.\n" +
            "......"));
    }

    [Test]
    public void Rectangle_OutlineAndFill_UsesBothColours()
    {
        using var document = Pixels.Blank(5, 5);
        var host = new TestHost(document) { Secondary = SKColors.Red };
        var rectangle = new RectangleTool { FillMode = ShapeFill.OutlineAndFill };

        rectangle.Press(host, At(0, 0));
        rectangle.Release(host, At(4, 4));

        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(16));
        Assert.That(Pixels.Count(document.Bitmap, SKColors.Red), Is.EqualTo(9));
    }

    [Test]
    public void Rectangle_WithShift_IsSquare()
    {
        using var document = Pixels.Blank(10, 10);
        var host = new TestHost(document);
        var rectangle = new RectangleTool { FillMode = ShapeFill.Filled };

        rectangle.Press(host, At(0, 0, shift: true));
        rectangle.Release(host, At(5, 2, shift: true));

        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(36));
    }

    [Test]
    public void Ellipse_FilledStaysInsideItsBounds()
    {
        using var document = Pixels.Blank(12, 12);
        var host = new TestHost(document);
        var ellipse = new EllipseTool { FillMode = ShapeFill.Filled };

        ellipse.Press(host, At(1, 1));
        ellipse.Release(host, At(10, 10));

        int inked = Pixels.Count(document.Bitmap, SKColors.Black);
        Assert.That(inked, Is.InRange(60, 100), "about π·5² pixels");
        Assert.That(document.Bitmap.GetPixel(0, 0), Is.EqualTo(SKColors.White));
        Assert.That(document.Bitmap.GetPixel(1, 1), Is.EqualTo(SKColors.White), "corners of the box are outside");
        Assert.That(document.Bitmap.GetPixel(5, 5), Is.EqualTo(SKColors.Black));
    }

    [Test]
    public void Line_WithShift_SnapsTo45Degrees()
    {
        using var document = Pixels.Blank(6, 6);
        var host = new TestHost(document);
        var line = new LineTool { Antialias = false }; // exact pixels along the diagonal

        line.Press(host, At(0, 0, shift: true));
        line.Release(host, At(5, 4, shift: true));

        Assert.That(Enumerable.Range(0, 5).All(i => document.Bitmap.GetPixel(i, i) == SKColors.Black), Is.True);
    }

    [Test]
    public void Fill_FloodsTheClickedRegion()
    {
        using var document = Pixels.Blank(4, 4);
        var host = new TestHost(document) { Primary = SKColors.Green };

        new FillTool().Press(host, At(2, 2));

        Assert.That(Pixels.Count(document.Bitmap, SKColors.Green), Is.EqualTo(16));
        document.Undo();
        Assert.That(Pixels.Count(document.Bitmap, SKColors.White), Is.EqualTo(16));
    }

    [Test]
    public void ColorPicker_SetsPrimaryOrSecondary()
    {
        using var document = Pixels.Blank(2, 1);
        document.Bitmap.SetPixel(1, 0, SKColors.Orange);
        var host = new TestHost(document);
        var picker = new ColorPickerTool();

        picker.Press(host, At(1, 0));
        Assert.That(host.Primary, Is.EqualTo(SKColors.Orange));

        picker.Press(host, At(0, 0, PointerButton.Secondary));
        Assert.That(host.Secondary, Is.EqualTo(SKColors.White));
    }

    [Test]
    public void Select_DragsOutASelection_ThenMovesIt()
    {
        using var document = Pixels.Blank(8, 4);
        document.Bitmap.SetPixel(1, 1, SKColors.Black);
        var host = new TestHost(document);
        var select = new SelectTool();

        select.Press(host, At(1, 1));
        select.Drag(host, At(2, 2));
        select.Release(host, At(2, 2));
        Assert.That(document.Selection, Is.EqualTo(new SKRectI(1, 1, 3, 3)));

        // Grab it by its top-left pixel and drop it four to the right.
        select.Press(host, At(1, 1));
        select.Drag(host, At(5, 1));
        select.Release(host, At(5, 1));
        Assert.That(document.HasFloating, Is.True);

        select.Deactivate(host);
        Assert.That(Pixels.Map(document.Bitmap, SKColors.Black), Is.EqualTo(
            "........\n" +
            ".....#..\n" +
            "........\n" +
            "........"));
        Assert.That(document.Selection, Is.Null);
    }

    [Test]
    public void Select_ClickWithoutDragging_Deselects()
    {
        using var document = Pixels.Blank();
        document.Select(new SKRectI(0, 0, 2, 2));
        var host = new TestHost(document);
        var select = new SelectTool();

        select.Press(host, At(6, 6));
        select.Release(host, At(6, 6));

        Assert.That(document.Selection, Is.Null);
    }

    [Test]
    public void Text_DrawsWhatWasTyped()
    {
        using var document = Pixels.Blank(60, 30);
        var host = new TestHost(document);
        var text = new TextTool { FontSize = 20, Antialias = false };

        text.DrawText(host, "Hi", new SKPointI(2, 2), SKColors.Black);

        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.GreaterThan(20));
        Assert.That(document.History.UndoName, Is.EqualTo("Text"));
    }

    [Test]
    public async Task Text_Cancelled_DrawsNothing()
    {
        using var document = Pixels.Blank();
        var host = new TestHost(document) { TextToType = null };

        new TextTool().Press(host, At(1, 1));
        await Task.Yield();

        Assert.That(document.History.CanUndo, Is.False);
    }
}
