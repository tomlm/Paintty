using SkiaSharp;

namespace Paintty.Tools;

/// <summary>
///     Drag to select a rectangle. Dragging inside the selection lifts its pixels (leaving the
///     secondary colour behind) and moves them; they go back down when the selection is committed.
/// </summary>
public sealed class SelectTool : ToolBase
{
    private enum Mode { None, Selecting, Moving }

    private Mode _mode;
    private SKPointI _anchor;
    private SKPointI _grab;
    private bool _moved;

    public override string Name => "Select";
    public override string Glyph => "⌗";
    public override char Hotkey => 'S';

    public override void Press(IToolHost host, ToolPointer pointer)
    {
        var document = host.Document;
        var pixel = pointer.Pixel;
        _moved = false;

        if (document.Selection is { } selection && selection.Contains(pixel.X, pixel.Y))
        {
            if (!document.HasFloating)
                document.LiftSelection(host.Secondary);
            _mode = Mode.Moving;
            _grab = new SKPointI(pixel.X - selection.Left, pixel.Y - selection.Top);
            return;
        }

        document.Deselect();
        _mode = Mode.Selecting;
        _anchor = pixel;
    }

    public override void Drag(IToolHost host, ToolPointer pointer)
    {
        var document = host.Document;
        var pixel = pointer.Pixel;
        switch (_mode)
        {
            case Mode.Selecting:
                _moved |= pixel != _anchor;
                if (_moved)
                    document.Select(new SKRectI(
                        Math.Min(_anchor.X, pixel.X), Math.Min(_anchor.Y, pixel.Y),
                        Math.Max(_anchor.X, pixel.X) + 1, Math.Max(_anchor.Y, pixel.Y) + 1));
                break;
            case Mode.Moving:
                document.MoveFloating(pixel.X - _grab.X, pixel.Y - _grab.Y);
                break;
        }
    }

    public override void Release(IToolHost host, ToolPointer pointer)
    {
        Drag(host, pointer);
        _mode = Mode.None;
    }

    public override void Cancel(IToolHost host)
    {
        _mode = Mode.None;
        host.Document.CommitFloating();
    }

    public override void Deactivate(IToolHost host)
    {
        _mode = Mode.None;
        host.Document.Deselect();
    }
}
