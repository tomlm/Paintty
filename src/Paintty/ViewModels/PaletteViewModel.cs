using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Paintty.Services;
using SkiaSharp;

namespace Paintty.ViewModels;

/// <summary>The primary and secondary colours and the swatches to pick them from, as in Paint.</summary>
public sealed partial class PaletteViewModel(IDialogService dialogs) : ObservableObject
{
    /// <summary>The classic 28-colour Paint palette: a dark row and a light row.</summary>
    public IReadOnlyList<SKColor> Swatches { get; } =
    [
        .. new uint[]
        {
            0xFF000000, 0xFF808080, 0xFF800000, 0xFF808000, 0xFF008000, 0xFF008080, 0xFF000080,
            0xFF800080, 0xFF808040, 0xFF004040, 0xFF0080FF, 0xFF004080, 0xFF8000FF, 0xFF804000,
            0xFFFFFFFF, 0xFFC0C0C0, 0xFFFF0000, 0xFFFFFF00, 0xFF00FF00, 0xFF00FFFF, 0xFF0000FF,
            0xFFFF00FF, 0xFFFFFF80, 0xFF00FF80, 0xFF80FFFF, 0xFF8080FF, 0xFFFF0080, 0xFFFF8040
        }.Select(argb => new SKColor(argb))
    ];

    [ObservableProperty] private SKColor _primary = SKColors.Black;
    [ObservableProperty] private SKColor _secondary = SKColors.White;

    [RelayCommand]
    private void SetPrimary(SKColor color) => Primary = color;

    [RelayCommand]
    private void SetSecondary(SKColor color) => Secondary = color;

    [RelayCommand]
    private void Swap() => (Primary, Secondary) = (Secondary, Primary);

    [RelayCommand]
    private void Reset() => (Primary, Secondary) = (SKColors.Black, SKColors.White);

    [RelayCommand]
    private async Task EditPrimaryAsync()
    {
        if (await dialogs.EditColorAsync(Primary) is { } color) Primary = color;
    }

    [RelayCommand]
    private async Task EditSecondaryAsync()
    {
        if (await dialogs.EditColorAsync(Secondary) is { } color) Secondary = color;
    }
}
