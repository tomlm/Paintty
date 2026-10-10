namespace Paintty.Tools;

/// <summary>
///     Drags the view around instead of drawing (H, as in Paint.NET). Panning is the view's business,
///     so the canvas view does it whenever this tool is selected; the tool itself never touches the image.
/// </summary>
public sealed class PanTool : ToolBase
{
    public override string Name => "Pan";
    public override string Glyph => "✥";
    public override char Hotkey => 'H';
}
