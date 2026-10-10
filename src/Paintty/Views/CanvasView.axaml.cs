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
///     Also pans the view: the Pan tool or the middle button drags it, Shift+wheel scrolls it
///     sideways (terminals report no horizontal wheel), and <see cref="PanBy" /> moves it from keys.
/// </summary>
public partial class CanvasView : UserControl
{
    private readonly ScrollViewer _scroller;
    private readonly Border _surface;
    private readonly Image _picture;
    private bool _fitted;

    /// <summary>While panning: where the drag started, in the scroller, and the offset then.</summary>
    private (Point From, Vector Offset)? _pan;

    public CanvasView()
    {
        AvaloniaXamlLoader.Load(this);
        _scroller = this.FindControl<ScrollViewer>("Scroller")!;
        _surface = this.FindControl<Border>("Surface")!;
        _picture = this.FindControl<Image>("Picture")!;

        _surface.PointerPressed += OnPointerPressed;
        _surface.PointerMoved += OnPointerMoved;
        _surface.PointerReleased += OnPointerReleased;
        _surface.PointerCaptureLost += (_, _) =>
        {
            _pan = null;
            ViewModel?.PointerCaptureLost();
        };
        _surface.PointerExited += (_, _) => ViewModel?.PointerExited();
        _scroller.AddHandler(PointerWheelChangedEvent, OnWheel, Avalonia.Interactivity.RoutingStrategies.Tunnel);
        _scroller.PropertyChanged += OnScrollerPropertyChanged;
    }

    private CanvasViewModel? ViewModel => DataContext as CanvasViewModel;

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;
        var properties = e.GetCurrentPoint(_surface).Properties;
        if (properties.IsMiddleButtonPressed || viewModel.Toolbox.SelectedTool is PanTool)
        {
            _pan = (e.GetPosition(_scroller), _scroller.Offset);
            e.Pointer.Capture(_surface);
            e.Handled = true;
            return;
        }

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
        if (_pan is { } pan)
        {
            // the image follows the pointer, so the offset moves against it
            var moved = e.GetPosition(_scroller) - pan.From;
            PanTo(pan.Offset.X - moved.X, pan.Offset.Y - moved.Y);
            return;
        }

        var properties = e.GetCurrentPoint(_surface).Properties;
        var button = properties.IsRightButtonPressed ? PointerButton.Secondary : PointerButton.Primary;
        viewModel.PointerMoved(ToImage(e, button));
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;
        if (_pan is not null)
        {
            _pan = null;
            e.Pointer.Capture(null);
            e.Handled = true;
            return;
        }

        var button = e.InitialPressMouseButton == MouseButton.Right ? PointerButton.Secondary : PointerButton.Primary;
        viewModel.PointerReleased(ToImage(e, button));
        e.Pointer.Capture(null);
        e.Handled = true;
    }

    /// <summary>Cells one notch of Shift+wheel scrolls sideways.</summary>
    private const int WheelStep = 4;

    /// <summary>
    ///     Ctrl+wheel zooms and Shift+wheel scrolls sideways; the plain wheel is left to the scroll viewer.
    /// </summary>
    private void OnWheel(object? sender, PointerWheelEventArgs e)
    {
        if (ViewModel is not { } viewModel) return;
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control))
            (e.Delta.Y > 0 ? viewModel.ZoomInCommand : viewModel.ZoomOutCommand).Execute(null);
        else if (e.KeyModifiers.HasFlag(KeyModifiers.Shift))
            PanBy(-Math.Sign(e.Delta.Y) * WheelStep, 0);
        else
            return;
        e.Handled = true;
    }

    /// <summary>
    ///     Moves the view by (<paramref name="columns" />, <paramref name="rows" />) cells; positive goes
    ///     right and down. It stops at the image's edges.
    /// </summary>
    public void PanBy(double columns, double rows) =>
        PanTo(_scroller.Offset.X + columns, _scroller.Offset.Y + rows);

    /// <summary>An arrow key's worth of panning: an eighth of the view, at least a cell.</summary>
    public void PanByArrow(Key key)
    {
        double columns = Math.Max(1, Math.Round(_scroller.Viewport.Width / 8));
        double rows = Math.Max(1, Math.Round(_scroller.Viewport.Height / 8));
        switch (key)
        {
            case Key.Left: PanBy(-columns, 0); break;
            case Key.Right: PanBy(columns, 0); break;
            case Key.Up: PanBy(0, -rows); break;
            case Key.Down: PanBy(0, rows); break;
        }
    }

    private void PanTo(double x, double y) =>
        _scroller.Offset = new Vector(
            Math.Clamp(x, 0, Math.Max(0, _scroller.Extent.Width - _scroller.Viewport.Width)),
            Math.Clamp(y, 0, Math.Max(0, _scroller.Extent.Height - _scroller.Viewport.Height)));

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
