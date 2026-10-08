using Avalonia.Data.Converters;
using Avalonia.Media;
using SkiaSharp;

namespace Paintty.Views;

/// <summary>Bridges SkiaSharp colours, which the view models speak, to Avalonia brushes.</summary>
public static class Converters
{
    public static Color ToAvalonia(this SKColor color) => Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue);

    public static readonly IValueConverter ColorToBrush =
        new FuncValueConverter<SKColor, IBrush>(color => new SolidColorBrush(color.ToAvalonia()));

    public static readonly IValueConverter ColorToHex =
        new FuncValueConverter<SKColor, string>(ViewModels.Dialogs.ColorEditorViewModel.ToHex);
}
