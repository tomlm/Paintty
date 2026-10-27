[![Build and Test](https://github.com/tomlm/Paintty/actions/workflows/BuildAndRunTests.yml/badge.svg)](https://github.com/tomlm/Paintty/actions/workflows/BuildAndRunTests.yml)[![NuGet](https://img.shields.io/nuget/v/Paintty.svg)](https://www.nuget.org/packages/Paintty)

![Logo](https://raw.githubusercontent.com/tomlm/Paintty/refs/heads/main/icon.png)

# Paintty

![Screenshot](https://raw.githubusercontent.com/tomlm/Paintty/refs/heads/main/screenshot.gif)

# Paintty — developer notes

An Avalonia MVVM app whose render backend is [Consolonia](https://github.com/consolonia/consolonia),
so it draws to the console.

## Building

Consolonia is referenced **from source**, from a sibling checkout at `../../../Consolonia`
(`S:\github\Consolonia`) on its `bitmapstuff` branch. That branch merges the pending Consolonia
PRs this app depends on: Sixel/Kitty bitmap rendering (#636), drawing writeable bitmaps at any
size (#690) and terminal pointer shapes (#692). Switch back to the `Consolonia` package once those
ship. Until then `GeneratePackageOnBuild` is off, because a package would depend on unpublished
Consolonia builds.

```
dotnet run --project src/Paintty -- [image]
dotnet test src/Paintty.slnx
```

Consolonia needs a real terminal, so run it from a terminal window rather than from an IDE's
output pane.

## How it fits together

The image is a SkiaSharp `SKBitmap` (BGRA, premultiplied), and **every pixel operation goes
through Skia**: `SKCanvas.DrawLine/DrawRect/DrawOval/DrawText`, colour filters, `SKBitmap.Resize`,
and `SKCodec`/`SKImage.Encode` for files. The one exception is flood fill, which Skia doesn't have.
It works on the pixel span directly.

Avalonia never touches the pixels. `CanvasViewModel` composites the document, any floating
selection, the selection marquee and the active tool's preview into one `SKBitmap`, then publishes
it as an Avalonia `Bitmap`. An ordinary `Image` binds to that, and Consolonia draws it as Sixel,
Kitty graphics or quadrant blocks, whichever the terminal supports. Zoom is just the `Image`'s
size. `IDisplayMetrics` says how many device pixels a cell holds, which is what 100% means.

| Path | Purpose |
|---|---|
| `Imaging/` | Pure SkiaSharp: bitmap helpers, flood fill, adjustments, the codec |
| `Editing/Document.cs` | The image, its selection and floating pixels; every change goes through it |
| `Editing/Edits.cs`, `History.cs` | Undo. An `EditScope` snapshots on open and records only the rectangle the tool touched |
| `Tools/` | One class per tool. Each gets pointer events in image coordinates through `IToolHost` |
| `Services/` | Dialogs, file pickers, codec and display metrics behind interfaces, wired up with DI in `App` |
| `ViewModels/` | CommunityToolkit.Mvvm view models: source-generated properties and (async) commands |
| `Views/` | XAML with compiled bindings. Code-behind only translates pointer and key input |
| `Views/Dialogs/DialogWindow` | One Consolonia `ManagedWindow` that hosts any `DialogViewModel`, chosen by data template |

To add a tool, derive from `ToolBase` (or `StrokeTool`/`ShapeTool`) and register it in
`App.ConfigureServices`. If it has options, add a `DataTemplate` in `Views/ToolOptionsView.axaml`.

## Tests

`Paintty.Tests` covers the imaging core, undo, every tool, and the view models with fake
dialogs. `AppTests` starts the real app on Consolonia's in-memory console and asserts on the
screen text.
