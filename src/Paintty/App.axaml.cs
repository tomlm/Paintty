using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using Iciclecreek.Avalonia.WindowManager;
using Microsoft.Extensions.DependencyInjection;
using Paintty.Editing;
using Paintty.Imaging;
using Paintty.Services;
using Paintty.Tools;
using Paintty.ViewModels;
using Paintty.Views;
using SkiaSharp;

namespace Paintty;

public partial class App : Application
{
    public const int DefaultWidth = 1024;
    public const int DefaultHeight = 1024;

    static App()
    {
        // Creating a WindowsPanel first stops it adding its own theme to the application's styles;
        // the managed-window styles on MainWindow supply it instead.
        _ = new WindowsPanel();
    }

    public IServiceProvider Services { get; private set; } = null!;

    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            Services = ConfigureServices(() => desktop.MainWindow);
            var viewModel = Services.GetRequiredService<MainWindowViewModel>();
            var window = new MainWindow { DataContext = viewModel };
            desktop.MainWindow = window;

            // paintty picture.png opens the picture.
            if (desktop.Args is [{ } path, ..] && File.Exists(path))
                window.Opened += async (_, _) => await viewModel.OpenFileAsync(path);
        }

        base.OnFrameworkInitializationCompleted();
    }

    private static ServiceProvider ConfigureServices(Func<TopLevel?> topLevel)
    {
        var services = new ServiceCollection();

        services.AddSingleton<IImageCodec, ImageCodec>();
        services.AddSingleton<IDialogService, DialogService>();
        services.AddSingleton<IFileDialogService>(provider =>
            new FileDialogService(topLevel, provider.GetRequiredService<IImageCodec>()));
        services.AddSingleton<IDisplayMetrics, ConsoleDisplayMetrics>();

        // The toolbox shows tools in registration order.
        services.AddSingleton<ToolBase, SelectTool>();
        services.AddSingleton<ToolBase, PencilTool>();
        services.AddSingleton<ToolBase, BrushTool>();
        services.AddSingleton<ToolBase, EraserTool>();
        services.AddSingleton<ToolBase, FillTool>();
        services.AddSingleton<ToolBase, ColorPickerTool>();
        services.AddSingleton<ToolBase, TextTool>();
        services.AddSingleton<ToolBase, LineTool>();
        services.AddSingleton<ToolBase, RectangleTool>();
        services.AddSingleton<ToolBase, EllipseTool>();

        services.AddSingleton<ToolboxViewModel>();
        services.AddSingleton<PaletteViewModel>();
        services.AddSingleton(provider => new CanvasViewModel(
            Document.CreateBlank(DefaultWidth, DefaultHeight, SKColors.White),
            provider.GetRequiredService<ToolboxViewModel>(),
            provider.GetRequiredService<PaletteViewModel>(),
            provider.GetRequiredService<IDialogService>(),
            provider.GetRequiredService<IDisplayMetrics>()));
        services.AddSingleton<MainWindowViewModel>();

        return services.BuildServiceProvider();
    }
}
