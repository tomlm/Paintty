using CommunityToolkit.Mvvm.ComponentModel;
using Paintty.Editing;
using Paintty.Imaging;
using SkiaSharp;

namespace Paintty.Tools;

/// <summary>
///     Freehand drawing: every drag segment is a Skia line from the last point, so a fast drag
///     leaves a continuous stroke rather than a trail of dots.
/// </summary>
public abstract class StrokeTool : ToolBase
{
    private EditScope? _edit;
    private SKPaint? _paint;
    private SKPoint _last;

    protected abstract float StrokeWidth { get; }
    protected abstract SKPaint CreatePaint(IToolHost host, PointerButton button);

    public override void Press(IToolHost host, ToolPointer pointer)
    {
        Cancel(host);
        _edit = host.Document.BeginEdit(Name);
        _paint = CreatePaint(host, pointer.Button);
        _last = pointer.Center;
        Segment(_last, _last);
    }

    public override void Drag(IToolHost host, ToolPointer pointer)
    {
        if (_edit is null) return;
        var point = pointer.Center;
        Segment(_last, point);
        _last = point;
    }

    public override void Release(IToolHost host, ToolPointer pointer)
    {
        Drag(host, pointer);
        _edit?.Commit();
        Reset();
    }

    public override void Cancel(IToolHost host)
    {
        _edit?.Revert();
        Reset();
    }

    public override void Deactivate(IToolHost host)
    {
        _edit?.Commit();
        Reset();
    }

    private void Segment(SKPoint from, SKPoint to)
    {
        var bitmap = _edit!.Document.Bitmap;
        using (var canvas = new SKCanvas(bitmap))
        {
            if (from == to)
                canvas.DrawPoint(from, _paint);
            else
                canvas.DrawLine(from, to, _paint);
        }

        _edit.Touch(new SKRect(from.X, from.Y, to.X, to.Y), StrokeWidth / 2);
    }

    private void Reset()
    {
        _paint?.Dispose();
        _paint = null;
        _edit = null;
    }
}

public sealed class PencilTool : StrokeTool
{
    public override string Name => "Pencil";
    public override string Glyph => "✎";
    public override char Hotkey => 'P';
    protected override float StrokeWidth => 1;

    protected override SKPaint CreatePaint(IToolHost host, PointerButton button) =>
        Paints.Stroke(ColorFor(host, button), 1, antialias: false, SKStrokeCap.Square);
}

public sealed partial class BrushTool : StrokeTool
{
    public override string Name => "Brush";
    public override string Glyph => "∫";
    public override char Hotkey => 'B';

    [ObservableProperty] private int _size = 4;
    [ObservableProperty] private bool _antialias = true;

    protected override float StrokeWidth => Size;

    public override bool TrySetWidth(int width)
    {
        Size = width;
        return true;
    }

    protected override SKPaint CreatePaint(IToolHost host, PointerButton button) =>
        Paints.Stroke(ColorFor(host, button), Size, Antialias);
}

/// <summary>Paints the secondary colour (primary with the right button), or clears to transparent.</summary>
public sealed partial class EraserTool : StrokeTool
{
    public override string Name => "Eraser";
    public override string Glyph => "⌫";
    public override char Hotkey => 'E';

    [ObservableProperty] private int _size = 8;
    [ObservableProperty] private bool _toTransparent;

    protected override float StrokeWidth => Size;

    public override bool TrySetWidth(int width)
    {
        Size = width;
        return true;
    }

    protected override SKPaint CreatePaint(IToolHost host, PointerButton button) =>
        Paints.Stroke(ToTransparent ? SKColors.Transparent : OtherColor(host, button), Size, antialias: false,
            SKStrokeCap.Square);
}
