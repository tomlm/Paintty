using CommunityToolkit.Mvvm.ComponentModel;
using Paintty.Imaging;
using SkiaSharp;

namespace Paintty.Tools;

public enum ShapeFill { Outline, Filled, OutlineAndFill }

/// <summary>
///     Drag out a shape: it is previewed in the overlay while the button is held and drawn onto the
///     image on release.
/// </summary>
public abstract partial class ShapeTool : ToolBase
{
    private SKPointI? _start;
    private SKPointI _end;
    private SKColor _stroke, _fill;
    private bool _shift;

    [ObservableProperty] private int _width = 1;
    [ObservableProperty] private bool _antialias = true;

    protected bool IsDragging => _start is not null;

    public override void Press(IToolHost host, ToolPointer pointer)
    {
        _start = _end = pointer.Pixel;
        _stroke = ColorFor(host, pointer.Button);
        _fill = OtherColor(host, pointer.Button);
        _shift = pointer.Shift;
        host.InvalidateOverlay();
    }

    public override void Drag(IToolHost host, ToolPointer pointer)
    {
        if (_start is null) return;
        _end = pointer.Pixel;
        _shift = pointer.Shift;
        host.InvalidateOverlay();
    }

    public override void Release(IToolHost host, ToolPointer pointer)
    {
        if (_start is not { } start) return;
        Drag(host, pointer);
        var end = Constrain(start, _end, _shift);

        using (var edit = host.Document.BeginEdit(Name))
        {
            using (var canvas = new SKCanvas(host.Document.Bitmap))
                Draw(canvas, start, end, _stroke, _fill);
            edit.Touch(Bounds(start, end), Width);
        }

        _start = null;
        host.InvalidateOverlay();
    }

    public override void Cancel(IToolHost host)
    {
        if (_start is null) return;
        _start = null;
        host.InvalidateOverlay();
    }

    public override void DrawOverlay(SKCanvas canvas)
    {
        if (_start is { } start)
            Draw(canvas, start, Constrain(start, _end, _shift), _stroke, _fill);
    }

    /// <summary>Draws the shape spanning the pixels <paramref name="start" /> to <paramref name="end" />, inclusive.</summary>
    protected abstract void Draw(SKCanvas canvas, SKPointI start, SKPointI end, SKColor stroke, SKColor fill);

    /// <summary>What Shift does to the second corner: square things up.</summary>
    protected virtual SKPointI Constrain(SKPointI start, SKPointI end, bool shift)
    {
        if (!shift) return end;
        int dx = end.X - start.X, dy = end.Y - start.Y;
        int side = Math.Max(Math.Abs(dx), Math.Abs(dy));
        return new SKPointI(start.X + Math.Sign(dx == 0 ? 1 : dx) * side, start.Y + Math.Sign(dy == 0 ? 1 : dy) * side);
    }

    /// <summary>The pixel rectangle from one corner pixel to the other, inclusive.</summary>
    protected static SKRect Bounds(SKPointI a, SKPointI b) =>
        new(Math.Min(a.X, b.X), Math.Min(a.Y, b.Y), Math.Max(a.X, b.X) + 1, Math.Max(a.Y, b.Y) + 1);
}

public sealed class LineTool : ShapeTool
{
    public override string Name => "Line";
    public override string Glyph => "╱";
    public override char Hotkey => 'L';

    protected override void Draw(SKCanvas canvas, SKPointI start, SKPointI end, SKColor stroke, SKColor fill)
    {
        using var paint = Paints.Stroke(stroke, Width, Antialias, Width == 1 ? SKStrokeCap.Square : SKStrokeCap.Round);
        canvas.DrawLine(start.X + 0.5f, start.Y + 0.5f, end.X + 0.5f, end.Y + 0.5f, paint);
    }

    /// <summary>Shift snaps the line to the nearest 45°.</summary>
    protected override SKPointI Constrain(SKPointI start, SKPointI end, bool shift)
    {
        if (!shift) return end;
        int dx = end.X - start.X, dy = end.Y - start.Y;
        double angle = Math.Round(Math.Atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
        double length = Math.Sqrt(dx * dx + dy * dy);
        // Diagonals keep |dx| = |dy| so they land on whole pixels.
        int ax = (int)Math.Round(Math.Cos(angle) * length), ay = (int)Math.Round(Math.Sin(angle) * length);
        if (ax != 0 && ay != 0) ax = Math.Sign(ax) * Math.Abs(ay);
        return new SKPointI(start.X + ax, start.Y + ay);
    }
}

public abstract partial class FilledShapeTool : ShapeTool
{
    [ObservableProperty] private ShapeFill _fillMode = ShapeFill.Outline;

    public static IReadOnlyList<ShapeFill> FillModes { get; } = Enum.GetValues<ShapeFill>();

    protected abstract void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint);

    protected override void Draw(SKCanvas canvas, SKPointI start, SKPointI end, SKColor stroke, SKColor fill)
    {
        var bounds = Bounds(start, end);
        if (FillMode != ShapeFill.Outline)
        {
            using var paint = Paints.Fill(FillMode == ShapeFill.Filled ? stroke : fill, Antialias);
            DrawShape(canvas, bounds, paint);
        }

        if (FillMode != ShapeFill.Filled)
        {
            // A stroke is centred on its path; inset by half its width so it stays inside the drag.
            var path = bounds;
            path.Inflate(-Width / 2f, -Width / 2f);
            using var paint = Paints.Stroke(stroke, Width, Antialias, SKStrokeCap.Square);
            DrawShape(canvas, path, paint);
        }
    }
}

public sealed class RectangleTool : FilledShapeTool
{
    public override string Name => "Rectangle";
    public override string Glyph => "□";
    public override char Hotkey => 'R';

    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) => canvas.DrawRect(bounds, paint);
}

public sealed class EllipseTool : FilledShapeTool
{
    public override string Name => "Ellipse";
    public override string Glyph => "○";
    public override char Hotkey => 'O';

    protected override void DrawShape(SKCanvas canvas, SKRect bounds, SKPaint paint) => canvas.DrawOval(bounds, paint);
}
