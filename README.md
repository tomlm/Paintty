# Paintty

A paint program for the terminal, in the spirit of Windows Paint and Paint.NET, built on
[Consolonia](https://github.com/consolonia/consolonia).

The image is drawn with real pixels when the terminal supports Sixel or Kitty graphics (Windows
Terminal, WezTerm, kitty, foot, …), and with quarter-cell block characters everywhere else.

```
paintty [image]
```

## What it does

* Open PNG, JPEG, BMP, GIF, WebP and ICO; save PNG, JPEG and WebP (lossless).
* Tools: select, pencil, brush, eraser, fill, colour picker, text, line, rectangle and ellipse.
* The left button draws with the primary colour, the right button with the secondary.
* Rectangle selection that can be moved, cut, copied, pasted, erased and cropped to.
* Resize, canvas size, flip, rotate, invert and black-and-white. Flips and the colour
  adjustments apply to the selection when there is one.
* Undo and redo, and a prompt before unsaved changes are lost.

## Keys

| Key | Does |
|---|---|
| `S` `P` `B` `E` `F` `K` `T` `L` `R` `O` | Select, Pencil, Brush, Eraser, Fill, Color picker, Text, Line, Rectangle, Ellipse |
| `X` | Swap the primary and secondary colours |
| `Shift` while dragging | Square rectangles and circles; lines snap to 45° |
| `Esc` | Abandon the current drag, or drop a floating selection |
| `Ctrl+N` `Ctrl+O` `Ctrl+S` `Ctrl+Shift+S` `Ctrl+Q` | New, Open, Save, Save As, Exit |
| `Ctrl+Z` `Ctrl+Y` | Undo, Redo |
| `Ctrl+X` `Ctrl+C` `Ctrl+V` `Del` | Cut, Copy, Paste, Erase selection |
| `Ctrl+A` `Ctrl+D` `Ctrl+Shift+X` | Select all, Deselect, Crop to selection |
| `Ctrl+R` `Ctrl+Shift+R` | Resize image, Canvas size |
| `Ctrl+H` `Ctrl+G` `Ctrl+J` | Rotate 90° clockwise, counter-clockwise, 180° |
| `Ctrl+I` `Ctrl+Shift+G` | Invert colours, Black and white |
| `Ctrl++` `Ctrl+-` `Ctrl+0` `Ctrl+B` | Zoom in, out, 100%, to window (also `Ctrl`+wheel) |
