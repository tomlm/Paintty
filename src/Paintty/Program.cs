using Avalonia;
using Consolonia;
using Consolonia.ManagedWindows.Storage;

namespace Paintty;

public static class Program
{
    [STAThread]
    private static void Main(string[] args) =>
        BuildAvaloniaApp().StartWithConsoleLifetime(args);

    public static AppBuilder BuildAvaloniaApp() =>
        AppBuilder.Configure<App>()
            // Skia does the bitmap work: decoding, encoding and every pixel the editor draws.
            .UseSkia()
            .UseConsoloniaStorage()
            .UseConsolonia()
            .UseAutoDetectedConsole()
            .LogToException();
}
