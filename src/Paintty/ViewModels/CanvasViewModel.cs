using System.ComponentModel;
using Avalonia;
using Avalonia.Platform;
using Avalonia.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Paintty.Editing;
using Paintty.Imaging;
using Paintty.Services;
using Paintty.Tools;
using SkiaSharp;
using AvaloniaBitmap = Avalonia.Media.Imaging.Bitmap;

namespace Paintty.ViewModels;

/// <summary>
///     The view onto the document: composites the image, the floating selection, the marquee and
///     the active tool's preview into one Skia bitmap, and publishes it as the Avalonia
///     <see cref="AvaloniaBitmap" /> an <c>Image</c> binds to. Pointer input comes back in image
///     coordinates and goes to the selected tool.
/// </summary>
public sealed partial class CanvasViewModel : ObservableObject, IToolHost, IDisposable
{
    public static IReadOnlyList<double> ZoomLevels { get; } =
        [0.05, 0.1, 0.125, 0.167, 0.25, 0.333, 0.5, 0.667, 1, 1.5, 2, 3, 4, 6, 8, 12, 16, 24, 32];

    private readonly IDialogService _dialogs;
    private readonly IDisplayMetrics _metrics;
    private readonly Action<Action> _post;
    private SKBitmap? _composite;
    private bool _renderQueued;
    private bool _pointerDown;
    private Func<SKBitmap, SKBitmap>? _previewTransform;
    private SKBitmap? _preview;
    private bool _previewStale;
    private bool _disposed;

    public CanvasViewModel(Document document, ToolboxViewModel toolbox, PaletteViewModel palette,
        IDialogService dialogs, IDisplayMetrics metrics, Action<Action>? post = null)
    {
        Toolbox = toolbox;
        Palette = palette;
        _dialogs = dialogs;
        _metrics = metrics;
        _post = post ?? (action => Dispatcher.UIThread.Post(action, DispatcherPriority.Render));
        _document = document;
        document.PropertyChanged += OnDocumentPropertyChanged;
        toolbox.Deselecting += tool => tool.Deactivate(this);
        toolbox.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ToolboxViewModel.SelectedTool)) InvalidateOverlay();
        };
        QueueRender();
    }

    public ToolboxViewModel Toolbox { get; }
    public PaletteViewModel Palette { get; }

    [ObservableProperty] private Document _document;

    /// <summary>What the <c>Image</c> shows. Replaced (never mutated) on every change.</summary>
    [ObservableProperty] private AvaloniaBitmap? _displayBitmap;

    /// <summary>Image pixels per device pixel: 1 is "100%", one image pixel to one terminal pixel.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayWidth), nameof(DisplayHeight), nameof(ZoomText))]
    private double _zoom = 1;

    /// <summary>Size of the area the image is shown in, in cells; set by the view.</summary>
    [ObservableProperty] private Size _viewportSize;

    /// <summary>The pixel under the pointer, or null when it is off the image.</summary>
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CursorText))]
    private SKPointI? _cursorPixel;

    /// <summary>How the terminal shows the image, and how many of its pixels a cell holds.</summary>
    public IDisplayMetrics DisplayMetrics => _metrics;

    /// <summary>How wide the image is drawn, in cells.</summary>
    public double DisplayWidth => (_preview?.Width ?? Document.Width) * Zoom / _metrics.PixelsPerCellX;

    /// <summary>How tall the image is drawn, in cells.</summary>
    public double DisplayHeight => (_preview?.Height ?? Document.Height) * Zoom / _metrics.PixelsPerCellY;

    /// <summary>Whether the canvas shows a preview of a transform rather than the document.</summary>
    public bool IsPreviewing => _previewTransform is not null;

    public string ZoomText => $"{Zoom * 100:0.#}%";

    public string CursorText => CursorPixel is { } p ? $"{p.X}, {p.Y}" : "";

    // ── IToolHost ──────────────────────────────────────────────────────────────────────────

    SKColor IToolHost.Primary
    {
        get => Palette.Primary;
        set => Palette.Primary = value;
    }

    SKColor IToolHost.Secondary
    {
        get => Palette.Secondary;
        set => Palette.Secondary = value;
    }

    public void InvalidateOverlay() => QueueRender();

    Task<string?> IToolHost.PromptTextAsync(string title) => _dialogs.PromptTextAsync(title);

    // ── pointer input, in image coordinates ─────────────────────────────────────────────────

    public void PointerPressed(ToolPointer pointer)
    {
        _pointerDown = true;
        UpdateCursor(pointer);
        Toolbox.SelectedTool.Press(this, pointer);
    }

    public void PointerMoved(ToolPointer pointer)
    {
        UpdateCursor(pointer);
        if (_pointerDown) Toolbox.SelectedTool.Drag(this, pointer);
    }

    public void PointerReleased(ToolPointer pointer)
    {
        if (!_pointerDown) return;
        _pointerDown = false;
        UpdateCursor(pointer);
        Toolbox.SelectedTool.Release(this, pointer);
    }

    public void PointerExited() => CursorPixel = null;

    /// <summary>
    ///     The pointer was taken away mid-gesture (a dialog opened, the window lost focus): abandon it.
    ///     Capture is also lost after every ordinary release, when there is nothing left to abandon.
    /// </summary>
    public void PointerCaptureLost()
    {
        if (!_pointerDown) return;
        CancelTool();
    }

    /// <summary>Esc: abandon whatever the tool is in the middle of.</summary>
    public void CancelTool()
    {
        _pointerDown = false;
        Toolbox.SelectedTool.Cancel(this);
    }

    private void UpdateCursor(ToolPointer pointer)
    {
        var p = pointer.Pixel;
        CursorPixel = p.X >= 0 && p.Y >= 0 && p.X < Document.Width && p.Y < Document.Height ? p : null;
    }

    // ── zoom ───────────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void ZoomIn() => Zoom = ZoomLevels.FirstOrDefault(level => level > Zoom * 1.001, ZoomLevels[^1]);

    [RelayCommand]
    private void ZoomOut() => Zoom = ZoomLevels.LastOrDefault(level => level < Zoom / 1.001, ZoomLevels[0]);

    [RelayCommand]
    private void ActualSize() => Zoom = 1;

    /// <summary>The largest zoom at which the whole image fits the viewport.</summary>
    [RelayCommand]
    private void ZoomToFit()
    {
        if (ViewportSize.Width <= 0 || ViewportSize.Height <= 0) return;
        double fitX = ViewportSize.Width * _metrics.PixelsPerCellX / Document.Width;
        double fitY = ViewportSize.Height * _metrics.PixelsPerCellY / Document.Height;
        Zoom = Math.Clamp(Math.Min(fitX, fitY), ZoomLevels[0], ZoomLevels[^1]);
    }

    // ── preview ────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Shows <paramref name="transform" /> applied to the image instead of the image itself (a
    ///     rotation being dialled in, say), or the image again when null. The transform runs when the
    ///     canvas next renders, so a slider dragged quickly costs one transform per frame, not per step.
    /// </summary>
    public void SetPreview(Func<SKBitmap, SKBitmap>? transform)
    {
        _previewTransform = transform;
        _previewStale = transform is not null;
        if (transform is null && _preview is not null)
        {
            _preview.Dispose();
            _preview = null;
            OnSizeChanged();
            return;
        }

        QueueRender();
    }

    // ── rendering ──────────────────────────────────────────────────────────────────────────

    partial void OnDocumentChanged(Document? oldValue, Document newValue)
    {
        if (oldValue is not null) oldValue.PropertyChanged -= OnDocumentPropertyChanged;
        newValue.PropertyChanged += OnDocumentPropertyChanged;
        _pointerDown = false;
        CursorPixel = null;
        OnSizeChanged();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        switch (e.PropertyName)
        {
            case nameof(Editing.Document.Revision):
                QueueRender();
                break;
            case nameof(Editing.Document.Bitmap):
                OnSizeChanged();
                break;
        }
    }

    private void OnSizeChanged()
    {
        OnPropertyChanged(nameof(DisplayWidth));
        OnPropertyChanged(nameof(DisplayHeight));
        QueueRender();
    }

    /// <summary>Coalesces however many changes arrive before the next frame into one render.</summary>
    private void QueueRender()
    {
        if (_renderQueued) return;
        _renderQueued = true;
        _post(() =>
        {
            _renderQueued = false;
            Render();
        });
    }

    internal void Render()
    {
        // a render queued just before the window closed would draw a disposed document
        if (_disposed) return;
        var document = Document;
        UpdatePreview();
        var shown = _preview ?? document.Bitmap;
        if (_composite is null || _composite.Width != shown.Width || _composite.Height != shown.Height)
        {
            _composite?.Dispose();
            _composite = new SKBitmap(Bitmaps.Info(shown.Width, shown.Height));
        }

        using (var canvas = new SKCanvas(_composite))
        {
            DrawCheckerboard(canvas, shown.Width, shown.Height);
            canvas.DrawBitmap(shown, 0, 0);

            // a preview shows a picture that isn't there yet, so nothing drawn over the image applies
            if (_preview is null)
            {
                if (document.Floating is { } floating && document.Selection is { } at)
                    canvas.DrawBitmap(floating, at.Left, at.Top);
                if (document.Selection is { } selection)
                    DrawMarquee(canvas, selection);
                Toolbox.SelectedTool.DrawOverlay(canvas);
            }
        }

        var old = DisplayBitmap;
        DisplayBitmap = new AvaloniaBitmap(PixelFormat.Bgra8888, AlphaFormat.Premul, _composite.GetPixels(),
            new PixelSize(_composite.Width, _composite.Height), new Vector(96, 96), _composite.RowBytes);
        old?.Dispose();
    }

    /// <summary>Makes the preview again if its transform changed since it was made.</summary>
    private void UpdatePreview()
    {
        if (!_previewStale || _previewTransform is null) return;
        _previewStale = false;
        var old = _preview;
        _preview = _previewTransform(Document.Bitmap);
        if (old is null || old.Width != _preview.Width || old.Height != _preview.Height)
        {
            OnPropertyChanged(nameof(DisplayWidth));
            OnPropertyChanged(nameof(DisplayHeight));
        }

        old?.Dispose();
    }

    /// <summary>A grey check under the image, so transparent pixels look transparent.</summary>
    private void DrawCheckerboard(SKCanvas canvas, int width, int height)
    {
        int cell = Math.Max(4, (int)(8 / Zoom));
        using var light = new SKPaint { Color = new SKColor(0xCC, 0xCC, 0xCC) };
        using var dark = new SKPaint { Color = new SKColor(0x99, 0x99, 0x99) };
        canvas.DrawRect(0, 0, width, height, light);
        for (int y = 0; y < height; y += cell)
        for (int x = (y / cell) % 2 * cell; x < width; x += cell * 2)
            canvas.DrawRect(x, y, cell, cell, dark);
    }

    /// <summary>Marching-ants style outline, thick enough to survive being scaled down for display.</summary>
    private void DrawMarquee(SKCanvas canvas, SKRectI selection)
    {
        float width = (float)Math.Max(1, 1 / Zoom);
        var rect = new SKRect(selection.Left, selection.Top, selection.Right, selection.Bottom);
        rect.Inflate(-width / 2, -width / 2);
        using var white = Paints.Stroke(SKColors.White, width, antialias: false, SKStrokeCap.Square);
        using var black = Paints.Stroke(SKColors.Black, width, antialias: false, SKStrokeCap.Square);
        using var dash = SKPathEffect.CreateDash([width * 4, width * 4], 0);
        black.PathEffect = dash;
        white.BlendMode = black.BlendMode = SKBlendMode.SrcOver;
        canvas.DrawRect(rect, white);
        canvas.DrawRect(rect, black);
    }

    public void Dispose()
    {
        _disposed = true;
        DisplayBitmap?.Dispose();
        _composite?.Dispose();
        _preview?.Dispose();
    }
}
