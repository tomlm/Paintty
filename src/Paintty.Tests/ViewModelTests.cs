using Paintty.Editing;
using Paintty.Imaging;
using Paintty.Services;
using Paintty.Tools;
using Paintty.ViewModels;
using Paintty.ViewModels.Dialogs;

namespace Paintty.Tests;

public class ViewModelTests
{
    private FakeDialogs _dialogs = null!;
    private FakeFiles _files = null!;
    private MainWindowViewModel _main = null!;

    [SetUp]
    public void SetUp()
    {
        _dialogs = new FakeDialogs();
        _files = new FakeFiles();
        var toolbox = new ToolboxViewModel([new SelectTool(), new PencilTool(), new BrushTool(), new FillTool(), new LineTool()]);
        var palette = new PaletteViewModel(_dialogs);
        // Rendering needs Avalonia's platform; these tests never let a render run.
        var canvas = new CanvasViewModel(Pixels.Blank(), toolbox, palette, _dialogs, new QuadMetrics(), post: _ => { });
        _main = new MainWindowViewModel(canvas, new ImageCodec(), _dialogs, _files);
    }

    [TearDown]
    public void TearDown() => _main.Dispose();

    [Test]
    public void UndoAndRedo_FollowTheHistory()
    {
        Assert.That(_main.UndoCommand.CanExecute(null), Is.False);

        Draw(_main, 1, 1);
        Assert.That(_main.UndoCommand.CanExecute(null), Is.True);
        Assert.That(_main.Title, Does.Contain("*"));

        _main.UndoCommand.Execute(null);
        Assert.That(_main.UndoCommand.CanExecute(null), Is.False);
        Assert.That(_main.RedoCommand.CanExecute(null), Is.True);
        Assert.That(_main.Title, Does.Not.Contain("*"));
    }

    [Test]
    public void SelectionCommands_NeedASelection()
    {
        Assert.That(_main.CopyCommand.CanExecute(null), Is.False);
        Assert.That(_main.PasteCommand.CanExecute(null), Is.False);

        _main.SelectAllCommand.Execute(null);
        Assert.That(_main.CopyCommand.CanExecute(null), Is.True);
        Assert.That(_main.Toolbox.SelectedTool, Is.InstanceOf<SelectTool>());

        _main.CopyCommand.Execute(null);
        Assert.That(_main.PasteCommand.CanExecute(null), Is.True);

        _main.DeselectCommand.Execute(null);
        Assert.That(_main.CopyCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void CutThenPaste_MovesPixels()
    {
        _main.Document.Bitmap.SetPixel(0, 0, SKColors.Black);
        _main.Document.Select(new SKRectI(0, 0, 1, 1));

        _main.CutCommand.Execute(null);
        Assert.That(Pixels.Count(_main.Document.Bitmap, SKColors.Black), Is.EqualTo(0));

        _main.PasteCommand.Execute(null);
        _main.Document.MoveFloating(3, 3);
        _main.EscapeCommand.Execute(null);
        Assert.That(_main.Document.Bitmap.GetPixel(3, 3), Is.EqualTo(SKColors.Black));
    }

    [Test]
    public async Task New_CleanDocument_DoesNotAsk()
    {
        _dialogs.NewSize = new ImageSize(20, 15);
        await _main.NewCommand.ExecuteAsync(null);

        Assert.That(_dialogs.SaveQuestions, Is.EqualTo(0));
        Assert.That((_main.Document.Width, _main.Document.Height), Is.EqualTo((20, 15)));
        Assert.That(_main.ImageSizeText, Is.EqualTo("20 × 15"));
    }

    [Test]
    public async Task New_DirtyDocument_Cancelled_KeepsIt()
    {
        Draw(_main, 0, 0);
        var before = _main.Document;
        _dialogs.SaveChoice = SaveChoice.Cancel;
        _dialogs.NewSize = new ImageSize(5, 5);

        await _main.NewCommand.ExecuteAsync(null);

        Assert.That(_dialogs.SaveQuestions, Is.EqualTo(1));
        Assert.That(_main.Document, Is.SameAs(before));
    }

    [Test]
    public async Task Exit_DirtyDocument_Discarded_Closes()
    {
        Draw(_main, 0, 0);
        _dialogs.SaveChoice = SaveChoice.Discard;
        bool closed = false;
        _main.CloseRequested += () => closed = true;

        await _main.ExitCommand.ExecuteAsync(null);

        Assert.That(closed, Is.True);
    }

    [Test]
    public async Task SaveThenOpen_RoundTrips()
    {
        string path = Path.Combine(Path.GetTempPath(), $"paintty-{Guid.NewGuid():N}.png");
        try
        {
            Draw(_main, 4, 5);
            _files.SavePath = path;
            await _main.SaveCommand.ExecuteAsync(null); // untitled, so this asks where

            Assert.That(_main.Document.IsDirty, Is.False);
            Assert.That(_main.Document.FilePath, Is.EqualTo(path));

            _files.OpenPath = path;
            await _main.OpenCommand.ExecuteAsync(null);
            Assert.That(_main.Document.Bitmap.GetPixel(4, 5), Is.EqualTo(SKColors.Black));
            Assert.That(_main.Title, Does.StartWith(Path.GetFileName(path)));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Open_BadFile_ShowsAnError()
    {
        string path = Path.GetTempFileName();
        File.WriteAllText(path, "nope");
        try
        {
            var before = _main.Document;
            Assert.That(await _main.OpenFileAsync(path), Is.False);
            Assert.That(_dialogs.Errors, Has.Count.EqualTo(1));
            Assert.That(_main.Document, Is.SameAs(before));
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task Resize_AppliesTheDialogAnswer()
    {
        _dialogs.Resize = new ResizeRequest(30, 20, Smooth: false);
        await _main.ResizeCommand.ExecuteAsync(null);
        Assert.That((_main.Document.Width, _main.Document.Height), Is.EqualTo((30, 20)));
    }

    [Test]
    public void Rotate_TakesDegreesAsTheParameter()
    {
        _main.Document.ApplyToImage("Crop", bitmap => bitmap.Copy(new SKRectI(0, 0, 4, 2)));
        _main.RotateCommand.Execute("90");
        Assert.That((_main.Document.Width, _main.Document.Height), Is.EqualTo((2, 4)));
    }

    [Test]
    public async Task RotateFree_AppliesTheChosenAngleAsOneUndoableEdit()
    {
        _dialogs.Rotate = new RotateRequest(30, Expand: true);

        await _main.RotateFreeCommand.ExecuteAsync(null);

        Assert.That((_main.Document.Width, _main.Document.Height), Is.EqualTo((14, 14)));
        _main.UndoCommand.Execute(null);
        Assert.That((_main.Document.Width, _main.Document.Height), Is.EqualTo((10, 10)));
    }

    [Test]
    public void Rotate_WithASelection_TurnsTheSelectionAndLeavesItFloating()
    {
        // a 4x2 bar at (2,2); a quarter turn makes it 2x4 about the same centre, (3,1)-(5,5)
        _main.Document.Bitmap.FillRect(new SKRectI(2, 2, 6, 4), SKColors.Black);
        _main.Document.Select(new SKRectI(2, 2, 6, 4));

        _main.RotateCommand.Execute("90");

        Assert.That((_main.Document.Width, _main.Document.Height), Is.EqualTo((10, 10)), "the image keeps its size");
        Assert.That(_main.Document.HasFloating, Is.True);
        Assert.That(_main.Document.Selection, Is.EqualTo(new SKRectI(3, 1, 5, 5)));

        _main.EscapeCommand.Execute(null);
        Assert.That(Pixels.Map(_main.Document.Bitmap, SKColors.Black).Split('\n')[1..5],
            Is.EqualTo(new[] { "...##.....", "...##.....", "...##.....", "...##....." }));
        Assert.That(_main.Document.Bitmap.GetPixel(2, 2), Is.EqualTo(_main.Palette.Secondary), "the lift leaves the secondary colour");

        _main.UndoCommand.Execute(null);
        Assert.That(Pixels.Count(_main.Document.Bitmap, SKColors.Black), Is.EqualTo(8));
        Assert.That(_main.Document.Bitmap.GetPixel(5, 3), Is.EqualTo(SKColors.Black), "undo puts the bar back");
    }

    [Test]
    public async Task RotateFree_WithASelection_TurnsOnlyTheSelection()
    {
        _main.Document.Select(new SKRectI(2, 2, 6, 6));
        _dialogs.Rotate = new RotateRequest(45, Expand: true);

        await _main.RotateFreeCommand.ExecuteAsync(null);

        Assert.That(_dialogs.RotatedSelection, Is.True);
        Assert.That((_main.Document.Width, _main.Document.Height), Is.EqualTo((10, 10)));
        // 4·cos45 + 4·sin45 = 5.66 → 6, centred on (4,4)
        Assert.That(_main.Document.Selection, Is.EqualTo(new SKRectI(1, 1, 7, 7)));
    }

    [Test]
    public void PreviewTransform_ShowsTheTurnedSelectionWithoutChangingTheImage()
    {
        _main.Document.Bitmap.FillRect(new SKRectI(2, 2, 6, 4), SKColors.Black);
        _main.Document.Select(new SKRectI(2, 2, 6, 4));

        using var preview = _main.Document.PreviewTransformSelectionOrImage(
            bitmap => Adjustments.Rotate(bitmap, 90), SKColors.White);

        Assert.That(preview.GetPixel(3, 1), Is.EqualTo(SKColors.Black));
        Assert.That(preview.GetPixel(2, 2), Is.EqualTo(SKColors.White));
        Assert.That(_main.Document.HasFloating, Is.False);
        Assert.That(_main.Document.Bitmap.GetPixel(2, 2), Is.EqualTo(SKColors.Black), "the image is untouched");
    }

    [Test]
    public async Task RotateFree_PreviewsWhileAskingAndStopsAfterwards()
    {
        bool previewedWhileAsking = false;
        _dialogs.RotatePreviews = [new RotateRequest(45, true)];
        _dialogs.WhileAsking = () => previewedWhileAsking = _main.Canvas.IsPreviewing;

        await _main.RotateFreeCommand.ExecuteAsync(null); // cancelled: Rotate is null

        Assert.That(previewedWhileAsking, Is.True);
        Assert.That(_main.Canvas.IsPreviewing, Is.False);
        Assert.That(_main.UndoCommand.CanExecute(null), Is.False, "cancelling changes nothing");
    }

    [Test]
    public async Task RotateFree_ZeroDegrees_ChangesNothing()
    {
        _dialogs.Rotate = new RotateRequest(0, true);
        await _main.RotateFreeCommand.ExecuteAsync(null);
        Assert.That(_main.UndoCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void RotateDialog_SliderAndBoxStayInStepAndPreview()
    {
        var previews = new List<RotateRequest>();
        var dialog = new RotateViewModel(previews.Add);

        dialog.SliderAngle = 44.6;
        Assert.That(dialog.Angle, Is.EqualTo(45m));
        dialog.Angle = -60;
        Assert.That(dialog.SliderAngle, Is.EqualTo(-60));
        dialog.Expand = false;

        Assert.That(previews, Is.EqualTo(new[]
        {
            new RotateRequest(45, true), new RotateRequest(-60, true), new RotateRequest(-60, false)
        }));
    }

    [Test]
    public void RotateDialog_RefusesAnglesOutOfRange()
    {
        var dialog = new RotateViewModel { Angle = 181 };
        Assert.That(dialog.AcceptCommand.CanExecute(null), Is.False);
        dialog.Angle = -181;
        Assert.That(dialog.AcceptCommand.CanExecute(null), Is.False);
        dialog.Angle = null;
        Assert.That(dialog.AcceptCommand.CanExecute(null), Is.False);
        dialog.Angle = -180;
        Assert.That(dialog.AcceptCommand.CanExecute(null), Is.True);
        dialog.Angle = 180;
        Assert.That(dialog.AcceptCommand.CanExecute(null), Is.True);
    }

    [Test]
    public async Task Keyboard_ListsEveryToolsHotkey()
    {
        await _main.KeyboardCommand.ExecuteAsync(null);

        var tools = _dialogs.Shortcuts!.Single(group => group.Name == "Tools").Shortcuts;
        foreach (var tool in _main.Toolbox.Tools)
            Assert.That(tools, Does.Contain(new Shortcut(tool.Hotkey.ToString(), tool.Name)));
    }

    /// <summary>
    ///     Every key binding in the main window is in the Keyboard table, so adding a shortcut without
    ///     listing it fails here.
    /// </summary>
    [Test]
    public void Keyboard_ListsEveryKeyBindingOfTheMainWindow()
    {
        string xaml = File.ReadAllText(SourcePath("Paintty", "Views", "MainWindow.axaml"));
        var gestures = System.Text.RegularExpressions.Regex
            .Matches(xaml, "<KeyBinding Gesture=\"([^\"]+)\"")
            .Select(match => match.Groups[1].Value.Replace("+D0", "+0")) // Avalonia's name for the 0 key
            .ToList();
        var listed = KeyboardShortcuts.For(_main.Toolbox).SelectMany(group => group.Shortcuts)
            .Select(shortcut => shortcut.Keys).ToHashSet();

        Assert.That(gestures, Is.Not.Empty);
        Assert.That(gestures.Where(gesture => !listed.Contains(gesture)), Is.Empty);
    }

    private static string SourcePath(params string[] parts) =>
        Path.Combine([Path.GetDirectoryName(ThisFile())!, "..", .. parts]);

    private static string ThisFile([System.Runtime.CompilerServices.CallerFilePath] string path = "") => path;

    [Test]
    public void Toolbox_HotkeysPickTools()
    {
        Assert.That(_main.Toolbox.TrySelect('b'), Is.True);
        Assert.That(_main.Toolbox.SelectedTool, Is.InstanceOf<BrushTool>());
        Assert.That(_main.Toolbox.TrySelect('Q'), Is.False);
    }

    [Test]
    public void Toolbox_DigitsSetTheWidthOfToolsThatHaveOne()
    {
        Assert.That(_main.Toolbox.TrySelect('L'), Is.True);
        Assert.That(_main.Toolbox.TrySetWidth(5), Is.True);
        Assert.That(_main.Toolbox.Get<LineTool>().Width, Is.EqualTo(5));

        _main.Toolbox.SelectedTool = _main.Toolbox.Get<BrushTool>();
        Assert.That(_main.Toolbox.TrySetWidth(7), Is.True);
        Assert.That(_main.Toolbox.Get<BrushTool>().Size, Is.EqualTo(7));

        _main.Toolbox.SelectedTool = _main.Toolbox.Get<PencilTool>();
        Assert.That(_main.Toolbox.TrySetWidth(3), Is.False);
    }

    [Test]
    public void SwitchingTools_FinishesTheOldOne()
    {
        _main.Document.PasteFloating(Bitmaps.Create(1, 1, SKColors.Black), 0, 0);
        _main.Toolbox.SelectedTool = _main.Toolbox.Get<SelectTool>();
        _main.Toolbox.SelectedTool = _main.Toolbox.Get<PencilTool>();

        Assert.That(_main.Document.HasFloating, Is.False);
        Assert.That(_main.Document.Bitmap.GetPixel(0, 0), Is.EqualTo(SKColors.Black));
    }

    [Test]
    public void Canvas_PointerInput_ReachesTheTool()
    {
        var canvas = _main.Canvas;
        _main.Toolbox.SelectedTool = _main.Toolbox.Get<PencilTool>();

        canvas.PointerPressed(TestHost.At(2, 2));
        canvas.PointerMoved(TestHost.At(2, 5));
        canvas.PointerReleased(TestHost.At(2, 5));

        Assert.That(Pixels.Count(_main.Document.Bitmap, SKColors.Black), Is.EqualTo(4));
        Assert.That(canvas.CursorText, Is.EqualTo("2, 5"));
    }

    [Test]
    public void Canvas_CaptureLostAfterRelease_KeepsTheMovedSelectionFloating()
    {
        var canvas = _main.Canvas;
        _main.Document.Select(new SKRectI(0, 0, 2, 2));
        _main.Toolbox.SelectedTool = _main.Toolbox.Get<SelectTool>();

        canvas.PointerPressed(TestHost.At(0, 0));
        canvas.PointerMoved(TestHost.At(3, 3));
        canvas.PointerReleased(TestHost.At(3, 3));
        canvas.PointerCaptureLost(); // Avalonia follows every release with this

        Assert.That(_main.Document.HasFloating, Is.True, "still floating, so it can be dragged again");
    }

    [Test]
    public void Canvas_CaptureLostMidStroke_AbandonsIt()
    {
        var canvas = _main.Canvas;
        _main.Toolbox.SelectedTool = _main.Toolbox.Get<PencilTool>();

        canvas.PointerPressed(TestHost.At(1, 1));
        canvas.PointerMoved(TestHost.At(5, 1));
        canvas.PointerCaptureLost();

        Assert.That(Pixels.Count(_main.Document.Bitmap, SKColors.Black), Is.EqualTo(0));
        Assert.That(_main.Document.History.CanUndo, Is.False);
    }

    [Test]
    public void Canvas_Zoom_SizesTheImageInCells()
    {
        var canvas = _main.Canvas; // 10×10 image, quad metrics: 2 px per cell across, 4 down
        canvas.Zoom = 1;
        Assert.That((canvas.DisplayWidth, canvas.DisplayHeight), Is.EqualTo((5.0, 2.5)));

        canvas.ZoomInCommand.Execute(null);
        Assert.That(canvas.Zoom, Is.EqualTo(1.5));

        canvas.ViewportSize = new Avalonia.Size(40, 20);
        canvas.ZoomToFitCommand.Execute(null);
        Assert.That(canvas.Zoom, Is.EqualTo(8), "the narrower fit: 40 cells × 2 px / 10 px");
    }

    [Test]
    public async Task Palette_EditAndSwap()
    {
        var palette = _main.Palette;
        _dialogs.Color = SKColors.Teal;
        await palette.EditPrimaryCommand.ExecuteAsync(null);
        Assert.That(palette.Primary, Is.EqualTo(SKColors.Teal));

        palette.SwapCommand.Execute(null);
        Assert.That((palette.Primary, palette.Secondary), Is.EqualTo((SKColors.White, SKColors.Teal)));
    }

    [Test]
    public async Task About_SaysHowTheTerminalShowsThePicture()
    {
        await _main.AboutCommand.ExecuteAsync(null);
        Assert.That(_dialogs.Messages.Single(), Does.Contain("Graphics: quadrant blocks"));
    }

    [Test]
    public void ResizeDialog_KeepsTheAspectRatio()
    {
        var resize = new ResizeViewModel(new ImageSize(200, 100)) { Width = 50 };
        Assert.That(resize.Height, Is.EqualTo(25));

        resize.KeepAspectRatio = false;
        resize.Width = 10;
        Assert.That(resize.Height, Is.EqualTo(25));

        resize.Width = 0;
        Assert.That(resize.AcceptCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void ColorEditor_HexAndChannelsStayInStep()
    {
        var editor = new ColorEditorViewModel(SKColors.Red);
        Assert.That(editor.Hex, Is.EqualTo("#FF0000"));

        editor.Green = 255;
        Assert.That(editor.Hex, Is.EqualTo("#FFFF00"));

        editor.Hex = "#800000FF";
        Assert.That((editor.Red, editor.Green, editor.Blue, editor.Alpha), Is.EqualTo((0, 0, 255, 128)));

        editor.Hex = "nonsense";
        Assert.That(editor.AcceptCommand.CanExecute(null), Is.False);
    }

    [Test]
    public void Dialog_AcceptAndCancel_ReportResults()
    {
        var dialog = new NewImageViewModel(new ImageSize(3, 4));
        object? result = "unset";
        dialog.CloseRequested += r => result = r;

        dialog.AcceptCommand.Execute(null);
        Assert.That(result, Is.EqualTo(new ImageSize(3, 4)));

        dialog.CancelCommand.Execute(null);
        Assert.That(result, Is.Null);
    }

    private static void Draw(MainWindowViewModel main, int x, int y)
    {
        main.Toolbox.SelectedTool = main.Toolbox.Get<PencilTool>();
        main.Canvas.PointerPressed(TestHost.At(x, y));
        main.Canvas.PointerReleased(TestHost.At(x, y));
    }
}
