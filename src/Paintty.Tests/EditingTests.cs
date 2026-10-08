using Paintty.Editing;
using Paintty.Imaging;

namespace Paintty.Tests;

public class EditingTests
{
    [Test]
    public void Edit_RecordsOnlyTheTouchedRegion_AndUndoRestoresIt()
    {
        using var document = Pixels.Blank();
        using (var edit = document.BeginEdit("Dot"))
        {
            document.Bitmap.SetPixel(3, 4, SKColors.Black);
            edit.Touch(new SKRectI(3, 4, 4, 5));
        }

        Assert.That(document.History.CanUndo, Is.True);
        Assert.That(document.History.UndoName, Is.EqualTo("Dot"));
        Assert.That(document.IsDirty, Is.True);

        document.Undo();
        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(0));
        Assert.That(document.IsDirty, Is.False, "back where it started");

        document.Redo();
        Assert.That(document.Bitmap.GetPixel(3, 4), Is.EqualTo(SKColors.Black));
        Assert.That(document.IsDirty, Is.True);
    }

    [Test]
    public void Edit_ThatTouchesNothing_IsNotRecorded()
    {
        using var document = Pixels.Blank();
        document.BeginEdit("Nothing").Commit();
        Assert.That(document.History.CanUndo, Is.False);
    }

    [Test]
    public void Revert_PutsThePixelsBack_WithoutHistory()
    {
        using var document = Pixels.Blank();
        var edit = document.BeginEdit("Scribble");
        document.Bitmap.FillRect(new SKRectI(0, 0, 5, 5), SKColors.Black);
        edit.Touch(new SKRectI(0, 0, 5, 5));
        edit.Revert();

        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(0));
        Assert.That(document.History.CanUndo, Is.False);
    }

    [Test]
    public void NewEdit_ClearsRedo()
    {
        using var document = Pixels.Blank();
        Paint(document, 0, 0);
        document.Undo();
        Assert.That(document.History.CanRedo, Is.True);

        Paint(document, 1, 1);
        Assert.That(document.History.CanRedo, Is.False);
    }

    [Test]
    public void Saving_ThenUndoing_IsDirtyAgain_AndRedoingIsClean()
    {
        using var document = Pixels.Blank();
        Paint(document, 0, 0);
        document.MarkSaved("saved.png");
        Assert.That(document.IsDirty, Is.False);
        Assert.That(document.DisplayName, Is.EqualTo("saved.png"));

        document.Undo();
        Assert.That(document.IsDirty, Is.True);
        document.Redo();
        Assert.That(document.IsDirty, Is.False);
    }

    [Test]
    public void History_DropsTheOldestEditsPastCapacity()
    {
        using var document = Pixels.Blank();
        for (int i = 0; i < document.History.Capacity + 5; i++)
            Paint(document, i % 10, i / 10 % 10);

        int undos = 0;
        while (document.History.CanUndo)
        {
            document.Undo();
            undos++;
        }

        Assert.That(undos, Is.EqualTo(document.History.Capacity));
    }

    [Test]
    public void ApplyToImage_CanChangeSize_AndUndoes()
    {
        using var document = Pixels.Blank(4, 2);
        document.ApplyToImage("Rotate", bitmap => Adjustments.Rotate(bitmap, 90));
        Assert.That((document.Width, document.Height), Is.EqualTo((2, 4)));

        document.Undo();
        Assert.That((document.Width, document.Height), Is.EqualTo((4, 2)));

        document.Redo();
        Assert.That((document.Width, document.Height), Is.EqualTo((2, 4)));
    }

    [Test]
    public void ApplyToSelectionOrImage_WithASelection_OnlyChangesTheSelection()
    {
        using var document = Pixels.Blank(4, 4);
        document.Select(new SKRectI(0, 0, 2, 2));
        document.ApplyToSelectionOrImage("Invert", Adjustments.Invert);

        Assert.That(Pixels.Map(document.Bitmap, SKColors.Black), Is.EqualTo(
            "##..\n" +
            "##..\n" +
            "....\n" +
            "...."));
    }

    [Test]
    public void Select_ClipsToTheImage()
    {
        using var document = Pixels.Blank(4, 4);
        document.Select(new SKRectI(2, 2, 10, 10));
        Assert.That(document.Selection, Is.EqualTo(new SKRectI(2, 2, 4, 4)));

        document.Select(new SKRectI(8, 8, 10, 10));
        Assert.That(document.Selection, Is.Null);
    }

    [Test]
    public void MovingASelection_LeavesTheHoleColour_AndIsOneUndo()
    {
        using var document = Pixels.Blank(6, 1);
        document.Bitmap.SetPixel(0, 0, SKColors.Black);
        document.Select(new SKRectI(0, 0, 1, 1));

        document.LiftSelection(SKColors.White);
        document.MoveFloating(4, 0);
        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(0), "floating, not yet on the image");

        document.CommitFloating();
        Assert.That(Pixels.Map(document.Bitmap, SKColors.Black), Is.EqualTo("....#."));
        Assert.That(document.Selection, Is.EqualTo(new SKRectI(4, 0, 5, 1)));

        document.Undo();
        Assert.That(Pixels.Map(document.Bitmap, SKColors.Black), Is.EqualTo("#....."));
        Assert.That(document.History.CanUndo, Is.False);
    }

    [Test]
    public void Paste_FloatsUntilCommitted_ThenUndoes()
    {
        using var document = Pixels.Blank(4, 4);
        document.PasteFloating(Bitmaps.Create(2, 2, SKColors.Black), 1, 1);
        Assert.That(document.HasFloating, Is.True);

        document.Deselect();
        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(4));
        Assert.That(document.History.UndoName, Is.EqualTo("Paste"));

        document.Undo();
        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(0));
    }

    [Test]
    public void Undo_WhileFloating_CommitsFirst()
    {
        using var document = Pixels.Blank(4, 4);
        document.PasteFloating(Bitmaps.Create(1, 1, SKColors.Black), 0, 0);
        document.Undo();

        Assert.That(document.HasFloating, Is.False);
        Assert.That(Pixels.Count(document.Bitmap, SKColors.Black), Is.EqualTo(0));
    }

    [Test]
    public void EraseSelection_FillsIt()
    {
        using var document = Document.CreateBlank(3, 3, SKColors.Black);
        document.Select(new SKRectI(1, 1, 3, 3));
        document.EraseSelection(SKColors.White);
        Assert.That(Pixels.Count(document.Bitmap, SKColors.White), Is.EqualTo(4));
    }

    [Test]
    public void CropToSelection_KeepsOnlyTheSelectedPixels()
    {
        using var document = Pixels.Blank(5, 5);
        document.Bitmap.SetPixel(2, 3, SKColors.Black);
        document.Select(new SKRectI(2, 2, 4, 4));

        document.CropToSelection();

        Assert.That((document.Width, document.Height), Is.EqualTo((2, 2)));
        Assert.That(Pixels.Map(document.Bitmap, SKColors.Black), Is.EqualTo("..\n#."));
        Assert.That(document.Selection, Is.Null);
    }

    [Test]
    public void Revision_ChangesWithEveryVisibleChange()
    {
        using var document = Pixels.Blank();
        int revision = document.Revision;
        Paint(document, 0, 0);
        Assert.That(document.Revision, Is.GreaterThan(revision));

        revision = document.Revision;
        document.Select(new SKRectI(0, 0, 2, 2));
        Assert.That(document.Revision, Is.GreaterThan(revision));
    }

    private static void Paint(Document document, int x, int y)
    {
        using var edit = document.BeginEdit("Paint");
        document.Bitmap.SetPixel(x, y, SKColors.Black);
        edit.Touch(new SKRectI(x, y, x + 1, y + 1));
    }
}
