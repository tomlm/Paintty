using Avalonia;
using Avalonia.Input;
using Avalonia.Input.Raw;
using Avalonia.Platform;
using Avalonia.Threading;
using Consolonia;
using Consolonia.Core.Drawing.PixelBufferImplementation;
using Consolonia.Core.Infrastructure;
using Consolonia.ManagedWindows.Storage;
using Consolonia.NUnit;

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
