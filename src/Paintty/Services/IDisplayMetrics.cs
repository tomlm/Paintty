using Consolonia;
using Consolonia.Controls;
using Consolonia.Core.Drawing.PixelBufferImplementation;

namespace Paintty.Services;

/// <summary>How many device pixels one console cell shows, which is what "100%" zoom is measured in.</summary>
public interface IDisplayMetrics
{
    double PixelsPerCellX { get; }
    double PixelsPerCellY { get; }

    /// <summary>How the terminal shows the picture, for people to read: "Kitty graphics", say.</summary>
    string Graphics { get; }
}

/// <summary>
///     With Sixel or Kitty graphics a cell shows the terminal's real cell size in pixels. Otherwise
///     Consolonia draws 2×2 quadrant blocks; a cell is about twice as tall as wide, so 2×4 keeps
///     image pixels square.
/// </summary>
public sealed class ConsoleDisplayMetrics : IDisplayMetrics
{
    public ConsoleDisplayMetrics()
    {
        if (ConsoloniaLifetime.Console is not IConsoleCapabilities console)
            return;

        // the same choice Consolonia makes when it picks a bitmap renderer: kitty needs RGB colour
        string? protocol =
            console.Capabilities.HasFlag(ConsoleCapabilities.SupportsKittyGraphics) &&
            ConsoloniaLifetime.ConsoleColorMode is RgbConsoleColorMode
                ? "Kitty graphics"
                : console.Capabilities.HasFlag(ConsoleCapabilities.SupportsSixel)
                    ? "Sixel"
                    : null;

        if (protocol != null && console.CellPixelWidth > 0 && console.CellPixelHeight > 0)
        {
            PixelsPerCellX = console.CellPixelWidth;
            PixelsPerCellY = console.CellPixelHeight;
            Graphics = $"{protocol}, {console.CellPixelWidth}×{console.CellPixelHeight}-pixel cells";
        }
    }

    public double PixelsPerCellX { get; } = 2;
    public double PixelsPerCellY { get; } = 4;
    public string Graphics { get; } = "quadrant blocks (no Sixel or Kitty graphics)";
}
