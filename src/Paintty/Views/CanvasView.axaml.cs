using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Paintty.Tools;
using Paintty.ViewModels;
using SkiaSharp;

namespace Paintty.Views;

/// <summary>
///     Turns pointer positions on the displayed image into image coordinates for the tools.
///     Zoom and aspect are all in the image's displayed size, so the mapping is just a ratio.
/// </summary>
public partial class CanvasView : UserControl
{
    private readonly ScrollViewer _scroller;
    private readonly Border _surface;
    private readonly Image _picture;
    private bool _fitted;

    public CanvasView()
    {
        AvaloniaXamlLoader.Load(this);
        _scroller = this.FindControl<ScrollViewer>("Scroller")!;
        _surface = this.FindControl<Border>("Surface")!;
        _picture = this.FindControl<Image>("Picture")!;

        _surface.PointerPressed += OnPointerPressed;
        _surface.PointerMoved += OnPointerMoved;
        _surface.PointerReleased += OnPointerReleased;
        _surface.PointerCaptureLost += (_, _) => ViewModel?.PointerCaptureLost();
        _surface.PointerExited += (_, _) => ViewModel?.PointerExited();
        _scroller.AddHandler(PointerWheelChangedEvent, OnWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _scroller.PropertyChanged += OnScrollerPropertyChanged;
    }

    private CanvasViewModel? ViewModel => DataContext as CanvasViewModel;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;
        var properties = e.GetCurrentPoint(_surface).Properties;
        var button = properties.IsRightButtonPressed ? PointerButton.Secondary
            : properties.IsLeftButtonPressed ? PointerButton.Primary
            : (PointerButton?)null;
        if (button is null) return;

        e.Pointer.Capture(_surface);
        viewModel.PointerPressed(ToImage(e, button.Value));
        e.Handled = true;
    }

    private void OnPointerMoved(object? sender, PointerEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;
        var properties = e.GetCurrentPoint(_surface).Properties;
        var button = properties.IsRightButtonPressed ? PointerButton.Secondary : PointerButton.Primary;
        viewModel.PointerMoved(ToImage(e, button));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;
        var button = e.InitialPressMouseButton == MouseButton.Right ? PointerButton.Secondary : PointerButton.Primary;
        viewModel.PointerReleased(ToImage(e, button));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>Ctrl+wheel zooms; the plain wheel is left to the scroll viewer.</summary>
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (ViewModel is not { } viewModel || !e.KeyModifiers.HasFlag(KeyModifiers.Control)) return;
        (e.Delta.Y > 0 ? viewModel.ZoomInCommand : viewModel.ZoomOutCommand).Execute(null);
        e.Handled = true;
    }

    private void OnScrollerPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property != ScrollViewer.ViewportProperty || ViewModel is not { } viewModel) return;
        viewModel.ViewportSize = _scroller.Viewport;

        // Start out showing the whole image, once there is a viewport to fit it to.
        if (!_fitted && _scroller.Viewport is { Width: > 0, Height: > 0 })
        {
            _fitted = true;
            viewModel.ZoomToFitCommand.Execute(null);
        }
    }

    private ToolPointer ToImage(PointerEventArgs e, PointerButton button)
    {
        var position = e.GetPosition(_picture);
        var size = _picture.Bounds.Size;
        var document = ViewModel!.Document;
        float x = size.Width > 0 ? (float)(position.X * document.Width / size.Width) : 0;
        float y = size.Height > 0 ? (float)(position.Y * document.Height / size.Height) : 0;
        return new ToolPointer(new SKPoint(x, y), button, e.KeyModifiers.HasFlag(KeyModifiers.Shift));
    }
}
