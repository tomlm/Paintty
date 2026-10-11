using CommunityToolkit.Mvvm.ComponentModel;
using Paintty.Editing;
using SkiaSharp;

namespace Paintty.Tools;

public enum PointerButton { Primary, Secondary }

/// <summary>A pointer event in image coordinates: (10.5, 3.5) is the centre of pixel (10, 3).</summary>
public readonly record struct ToolPointer(SKPoint Position, PointerButton Button, bool Shift = false)
{
    public SKPointI Pixel => new((int)MathF.Floor(Position.X), (int)MathF.Floor(Position.Y));

    /// <summary>The centre of the pixel under the pointer, where crisp 1-pixel strokes line up.</summary>
    public SKPoint Center => new(Pixel.X + 0.5f, Pixel.Y + 0.5f);
}

/// <summary>What a tool can reach of the editor around it.</summary>
public interface IToolHost
{
    Document Document { get; }
    SKColor Primary { get; set; }
    SKColor Secondary { get; set; }

    /// <summary>The tool's overlay changed; redraw it.</summary>
    void InvalidateOverlay();

    /// <summary>Asks the user for a line of text; null when they cancel.</summary>
    Task<string?> PromptTextAsync(string title);
}

/// <summary>
///     A tool in the toolbox. It gets pointer events in image coordinates and draws on the document
///     through Skia; anything it wants shown but not yet committed it draws in <see cref="DrawOverlay" />.
/// </summary>
public abstract class ToolBase : ObservableObject
{
    public abstract string Name { get; }

    /// <summary>The single character shown on the toolbox button.</summary>
    public abstract string Glyph { get; }

    /// <summary>The key that selects the tool.</summary>
    public abstract char Hotkey { get; }

    public string ToolTip => $"{Name} ({Hotkey})";

    /// <summary>
    ///     A typed digit while the tool is selected: tools that draw with a stroke width take it as
    ///     the new width and return true; the rest ignore it.
    /// </summary>
    public virtual bool TrySetWidth(int width) => false;

    public virtual void Press(IToolHost host, ToolPointer pointer)
    {
    }

    public virtual void Drag(IToolHost host, ToolPointer pointer)
    {
    }

    public virtual void Release(IToolHost host, ToolPointer pointer)
    {
    }

    /// <summary>Esc while the tool is busy: abandon what it was doing.</summary>
    public virtual void Cancel(IToolHost host)
    {
    }

    /// <summary>Another tool was picked; finish anything in flight.</summary>
    public virtual void Deactivate(IToolHost host) => Cancel(host);

    /// <summary>Draws in image coordinates over the composited image; nothing here is part of it.</summary>
    public virtual void DrawOverlay(SKCanvas canvas)
    {
    }

    protected static SKColor ColorFor(IToolHost host, PointerButton button) =>
        button == PointerButton.Primary ? host.Primary : host.Secondary;

    protected static SKColor OtherColor(IToolHost host, PointerButton button) =>
        button == PointerButton.Primary ? host.Secondary : host.Primary;
}
