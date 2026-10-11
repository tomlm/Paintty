using Paintty.Services;

namespace Paintty.ViewModels;

/// <summary>
///     Every key and mouse shortcut, grouped as the menus are, for Help → Keyboard. The tool keys come
///     from the toolbox, so a new tool shows up by itself. A test checks every key binding of the main
///     window is listed here.
/// </summary>
public static class KeyboardShortcuts
{
    public static IReadOnlyList<ShortcutGroup> For(ToolboxViewModel toolbox) =>
    [
        new("Tools",
        [
            .. toolbox.Tools.Select(tool => new Shortcut(tool.Hotkey.ToString(), tool.Name)),
            new("X", "Swap the primary and secondary colours"),
            new("1 – 9", "Width of the brush, eraser, line, rectangle or ellipse"),
            new("Shift while dragging", "Square rectangles and circles; lines snap to 45°"),
            new("Esc", "Abandon the current drag, or drop a floating selection")
        ]),
        new("File",
        [
            new("Ctrl+N", "New"),
            new("Ctrl+O", "Open"),
            new("Ctrl+S", "Save"),
            new("Ctrl+Shift+S", "Save As"),
            new("Ctrl+Q", "Exit")
        ]),
        new("Edit",
        [
            new("Ctrl+Z", "Undo"),
            new("Ctrl+Y", "Redo"),
            new("Ctrl+X", "Cut"),
            new("Ctrl+C", "Copy"),
            new("Ctrl+V", "Paste"),
            new("Delete", "Erase selection"),
            new("Ctrl+A", "Select all"),
            new("Ctrl+D", "Deselect")
        ]),
        new("Image",
        [
            new("Ctrl+Shift+X", "Crop to selection"),
            new("Ctrl+R", "Resize"),
            new("Ctrl+Shift+R", "Canvas size"),
            new("Ctrl+H", "Rotate 90° clockwise"),
            new("Ctrl+G", "Rotate 90° counter-clockwise"),
            new("Ctrl+J", "Rotate 180°"),
            new("Ctrl+Shift+H", "Rotate by any angle (the selection, when there is one)")
        ]),
        new("Adjustments",
        [
            new("Ctrl+I", "Invert colours"),
            new("Ctrl+Shift+G", "Black and white")
        ]),
        new("View",
        [
            new("+", "Zoom in"),
            new("-", "Zoom out"),
            new("Ctrl+0", "Actual size"),
            new("Ctrl+B", "Zoom to window"),
            new("Arrow keys", "Pan")
        ]),
        new("Mouse",
        [
            new("Left / right button", "Draw with the primary / secondary colour"),
            new("Wheel", "Scroll up and down"),
            new("Shift+wheel", "Scroll left and right"),
            new("Ctrl+wheel", "Zoom"),
            new("Middle-drag", "Pan, with any tool")
        ])
    ];
}
