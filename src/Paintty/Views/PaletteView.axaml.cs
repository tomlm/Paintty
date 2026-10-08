using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Paintty.ViewModels;
using SkiaSharp;

namespace Paintty.Views;

public partial class PaletteView : UserControl
{
    public PaletteView()
    {
        AvaloniaXamlLoader.Load(this);
        this.FindControl<ItemsControl>("Swatches")!.PointerPressed += OnSwatchPressed;
    }

    /// <summary>Which button picked the swatch decides which colour it sets — not something a Command can tell.</summary>
    private void OnSwatchPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is not PaletteViewModel palette
            || (e.Source as Control)?.GetSelfAndVisualAncestors().OfType<Control>()
                .FirstOrDefault(control => control.DataContext is SKColor)?.DataContext is not SKColor color)
            return;

        var properties = e.GetCurrentPoint(this).Properties;
        if (properties.IsRightButtonPressed)
            palette.SetSecondaryCommand.Execute(color);
        else if (properties.IsLeftButtonPressed)
            palette.SetPrimaryCommand.Execute(color);
        e.Handled = true;
    }
}
