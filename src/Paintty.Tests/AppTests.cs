using System.Reflection;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Consolonia;
using Consolonia.Core.Drawing.PixelBufferImplementation;
using Consolonia.Core.Infrastructure;
using Consolonia.ManagedWindows.Storage;
using Consolonia.NUnit;
using Paintty.ViewModels;

namespace Paintty.Tests;

/// <summary>
///     Starts the real app on Consolonia's in-memory console and checks what reaches the screen:
///     the XAML loads, the bindings resolve, and the keyboard drives the view models.
/// </summary>
public class AppTests() : ConsoloniaAppTestBase<App>(new PixelBufferSize(120, 40))
{
    /// <summary>The base builder plus Skia, which has to come first, as it does in <see cref="Program" />.</summary>
    protected override AppBuilder CreateAppBuilder() =>
        AppBuilder.Configure<App>()
            .UseSkia()
            .UseConsoloniaStorage()
            .UseConsole(UITest)
            .UseConsolonia()
            .UseConsoleColorMode(new RgbConsoleColorMode())
            .With<IPlatformSettings>(new ConsoloniaPlatformSettings { UnsafeInput = false, UnsafeRendering = false })
            .LogToException();

    [Test]
    public async Task StartsWithABlankImage_AndTheToolbox()
    {
        await AssertScreenHas("File", "Edit", "Image", "Adjustments", "View", "Help");
        await AssertScreenHas("Pencil", "Brush", "Eraser", "Fill", "Text", "Rectangle", "Ellipse");
        await AssertScreenHas("1024 × 1024", "Untitled");
    }

    [Test]
    public async Task ToolHotkeys_ShowThatToolsOptions()
    {
        await UITest.KeyInput(Key.B);
        await AssertScreenHas("Size", "Antialias");

        await UITest.KeyInput(Key.F);
        await AssertScreenHas("Tolerance");
    }

    [Test]
    public async Task CtrlN_AsksForTheNewImageSize()
    {
        await UITest.KeyInput(Key.N, RawInputModifiers.Control);
        await AssertScreenHas("New Image", "Width", "Height");

        await UITest.KeyInput(Key.Escape);
        await AssertScreenLacks("New Image");
    }

    [Test]
    public async Task NewImage_TypedSize_IsCommittedByEnter()
    {
        await UITest.KeyInput(Key.N, RawInputModifiers.Control);
        await AssertScreenHas("New Image");

        await UITest.KeyInput(Key.A, RawInputModifiers.Control);
        await UITest.StringInput("200");
        await UITest.KeyInput(Key.Tab);
        await UITest.KeyInput(Key.A, RawInputModifiers.Control);
        await UITest.StringInput("100");
        await UITest.KeyInput(Key.Enter);

        await AssertScreenLacks("New Image");
        await AssertScreenHas("200 × 100");
    }

    [Test]
    public async Task Brackets_ZoomInAndOut()
    {
        double before = await OnUi(() => MainViewModel.Canvas.Zoom);

        await UITest.KeyInput(Key.OemOpenBrackets);
        Assert.That(await OnUi(() => MainViewModel.Canvas.Zoom), Is.GreaterThan(before));

        await UITest.KeyInput(2, Key.OemCloseBrackets);
        Assert.That(await OnUi(() => MainViewModel.Canvas.Zoom), Is.LessThan(before));
    }

    [Test]
    public async Task CtrlWheel_Zooms()
    {
        double before = await OnUi(() => MainViewModel.Canvas.Zoom);
        await MouseAsync(RawPointerEventType.Wheel, CanvasPoint, new Vector(0, 1), RawInputModifiers.Control);
        Assert.That(await OnUi(() => MainViewModel.Canvas.Zoom), Is.GreaterThan(before));
    }

    [Test]
    public async Task Brackets_TypedInATextBox_DoNotZoom()
    {
        await UITest.KeyInput(Key.T); // the text tool's options have a font size box
        await AssertScreenHas("Bold");
        Assert.That(await OnUi(() =>
        {
            var window = ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
            return window.GetVisualDescendants().OfType<NumericUpDown>().Single(box => box.Name == "FontSize")
                .GetVisualDescendants().OfType<TextBox>().First().Focus();
        }), Is.True);
        double before = await OnUi(() => MainViewModel.Canvas.Zoom);

        await UITest.KeyInput(Key.OemOpenBrackets);

        Assert.That(await OnUi(() => MainViewModel.Canvas.Zoom), Is.EqualTo(before));
    }

    [Test]
    public async Task ArrowKeys_PanTheZoomedImage()
    {
        await ZoomInAsync();
        Assert.That(await OffsetAsync(), Is.EqualTo(new Vector(0, 0)));

        await UITest.KeyInput(Key.Right);
        await UITest.KeyInput(Key.Down);
        var panned = await OffsetAsync();
        Assert.That(panned.X, Is.GreaterThan(0));
        Assert.That(panned.Y, Is.GreaterThan(0));

        await UITest.KeyInput(Key.Left);
        Assert.That((await OffsetAsync()).X, Is.EqualTo(0));
    }

    [Test]
    public async Task ArrowKeys_StillDriveAnOpenMenu()
    {
        await ZoomInAsync();
        // the test console's keys don't reach the menu's access keys, so open it directly
        await OnUi(() =>
        {
            var window = ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
            var file = window.GetVisualDescendants().OfType<MenuItem>().First();
            file.Focus();
            file.IsSubMenuOpen = true;
            return true;
        });
        await AssertScreenHas("Save As");
        await UITest.KeyInput(Key.Down);
        await AssertScreenHas("Save As");
        Assert.That(await OffsetAsync(), Is.EqualTo(new Vector(0, 0)));
        await UITest.KeyInput(Key.Escape);
    }

    [Test]
    public async Task ShiftWheel_PansSideways()
    {
        await ZoomInAsync();

        await MouseAsync(RawPointerEventType.Wheel, CanvasPoint, new Vector(0, -1), RawInputModifiers.Shift);
        double right = (await OffsetAsync()).X;
        Assert.That(right, Is.GreaterThan(0));

        await MouseAsync(RawPointerEventType.Wheel, CanvasPoint, new Vector(0, 1), RawInputModifiers.Shift);
        Assert.That((await OffsetAsync()).X, Is.LessThan(right));
        Assert.That((await OffsetAsync()).Y, Is.EqualTo(0), "Shift+wheel does not scroll vertically");
    }

    [Test]
    public async Task PanTool_DragMovesTheView_AndDrawsNothing()
    {
        await ZoomInAsync();
        await UITest.KeyInput(Key.H);
        await AssertScreenHas("Drag to move the view");

        await DragAsync(RawPointerEventType.LeftButtonDown, RawPointerEventType.LeftButtonUp,
            RawInputModifiers.LeftMouseButton);

        Assert.That(await OffsetAsync(), Is.EqualTo(new Vector(10, 5)));
        Assert.That(await OnUi(() => MainViewModel.Document.IsDirty), Is.False);
    }

    [Test]
    public async Task MiddleDrag_PansWithAnyTool_AndDrawsNothing()
    {
        await ZoomInAsync(); // the pencil is selected at start

        await DragAsync(RawPointerEventType.MiddleButtonDown, RawPointerEventType.MiddleButtonUp,
            RawInputModifiers.MiddleMouseButton);

        Assert.That(await OffsetAsync(), Is.EqualTo(new Vector(10, 5)));
        Assert.That(await OnUi(() => MainViewModel.Document.IsDirty), Is.False);
    }

    [Test]
    public async Task HelpKeyboard_ShowsTheShortcuts()
    {
        await OnUi(() =>
        {
            MainViewModel.KeyboardCommand.Execute(null);
            return true;
        });
        await AssertScreenHas("Keyboard", "Tools", "Swap the primary", "Ctrl+Shift+S", "Save As");
        await AssertScreenLacks("Cancel");

        await UITest.KeyInput(3, Key.PageDown);
        await AssertScreenHas("Middle-drag");

        await UITest.KeyInput(Key.Escape);
        await AssertScreenLacks("Ctrl+Shift+S");
    }

    [Test]
    public async Task CtrlShiftH_OpensTheRotateDialog()
    {
        await UITest.KeyInput(Key.H, RawInputModifiers.Control | RawInputModifiers.Shift);
        await AssertScreenHas("Rotate", "Angle", "Expand canvas to fit");

        await UITest.KeyInput(Key.Escape);
        await AssertScreenLacks("Expand canvas to fit");
    }

    /// <summary>A point well inside the canvas, right of the toolbox and above the palette.</summary>
    private static readonly Point CanvasPoint = new(60, 20);

    private static MainWindowViewModel MainViewModel =>
        (MainWindowViewModel)((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!)
        .MainWindow!.DataContext!;

    private static async Task<T> OnUi<T>(Func<T> read) => await Dispatcher.UIThread.InvokeAsync(read);

    /// <summary>Zooms in far enough that the image is larger than the view both ways.</summary>
    private async Task ZoomInAsync()
    {
        await UITest.KeyInput(6, Key.OemOpenBrackets);
        await UITest.WaitRendered();
    }

    private async Task<Vector> OffsetAsync()
    {
        await UITest.WaitRendered();
        return await OnUi(() =>
        {
            var window = ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow!;
            return window.GetVisualDescendants().OfType<ScrollViewer>().Single(s => s.Name == "Scroller").Offset;
        });
    }

    /// <summary>Presses at <see cref="CanvasPoint" />, drags 10 cells left and 5 up, and lets go.</summary>
    private async Task DragAsync(RawPointerEventType down, RawPointerEventType up, RawInputModifiers held)
    {
        await MouseAsync(down, CanvasPoint, null, held);
        await MouseAsync(RawPointerEventType.Move, CanvasPoint - new Point(5, 2), null, held);
        await MouseAsync(RawPointerEventType.Move, CanvasPoint - new Point(10, 5), null, held);
        await MouseAsync(up, CanvasPoint - new Point(10, 5), null, RawInputModifiers.None);
    }

    /// <summary>
    ///     Sends a mouse event the way a console reports one. UnitTestConsole has no helper for the
    ///     mouse, so this raises its MouseEvent, and the event goes through Avalonia's input like a real one.
    /// </summary>
    private async Task MouseAsync(RawPointerEventType type, Point point, Vector? wheel, RawInputModifiers modifiers)
    {
        var field = typeof(UnitTestConsole).GetField(nameof(UnitTestConsole.MouseEvent),
            BindingFlags.Instance | BindingFlags.NonPublic)!;
        await Dispatcher.UIThread.InvokeAsync(() =>
            ((Action<RawPointerEventType, Point, Vector?, RawInputModifiers>?)field.GetValue(UITest))
                ?.Invoke(type, point, wheel, modifiers));
        await UITest.WaitRendered();
    }

    /// <summary>
    ///     What is on screen. Consolonia.NUnit has AssertHasText for this, but it is built on NUnit 3's
    ///     classic asserts, which NUnit 4 no longer has.
    /// </summary>
    private async Task<string> ScreenAsync()
    {
        await UITest.WaitRendered();
        return await Dispatcher.UIThread.InvokeAsync(() => UITest.PixelBuffer.PrintBuffer());
    }

    private async Task AssertScreenHas(params string[] texts)
    {
        string screen = await ScreenAsync();
        foreach (string text in texts)
            Assert.That(screen, Does.Contain(text), $"'{text}' is not on screen:\n{screen}");
    }

    private async Task AssertScreenLacks(params string[] texts)
    {
        string screen = await ScreenAsync();
        foreach (string text in texts)
            Assert.That(screen, Does.Not.Contain(text), $"'{text}' is still on screen:\n{screen}");
    }
}
