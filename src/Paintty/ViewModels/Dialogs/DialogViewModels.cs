using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Paintty.Imaging;
using Paintty.Services;
using SkiaSharp;

namespace Paintty.ViewModels.Dialogs;

/// <summary>What the dialog window binds to, whatever the dialog.</summary>
public interface IDialogViewModel
{
    string Title { get; }
    IRelayCommand AcceptCommand { get; }
    IRelayCommand CancelCommand { get; }

    /// <summary>Whether there is anything to cancel; a dialog that only shows something has just OK.</summary>
    bool CanCancel { get; }

    /// <summary>Raised with the dialog's result (null on cancel) when it should close.</summary>
    event Action<object?>? CloseRequested;
}

public abstract partial class DialogViewModel<TResult> : ObservableObject, IDialogViewModel
{
    public abstract string Title { get; }

    public event Action<object?>? CloseRequested;

    public virtual bool CanCancel => true;

    protected virtual bool CanAccept() => true;

    protected abstract TResult CreateResult();

    [RelayCommand(CanExecute = nameof(CanAccept))]
    private void Accept() => CloseRequested?.Invoke(CreateResult());

    [RelayCommand]
    private void Cancel() => CloseRequested?.Invoke(null);
}

/// <summary>Bounds for any image dimension the dialogs accept.</summary>
public static class Limits
{
    public const int MaxDimension = 16384;

    public static bool IsValid(decimal? value) => value is >= 1 and <= MaxDimension;
}

public sealed partial class NewImageViewModel(ImageSize suggested) : DialogViewModel<ImageSize?>
{
    public override string Title => "New Image";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private decimal? _width = suggested.Width;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private decimal? _height = suggested.Height;

    protected override bool CanAccept() => Limits.IsValid(Width) && Limits.IsValid(Height);

    protected override ImageSize? CreateResult() => new((int)Width!.Value, (int)Height!.Value);
}

public sealed partial class ResizeViewModel : DialogViewModel<ResizeRequest?>
{
    private readonly ImageSize _original;
    private bool _syncing;

    public ResizeViewModel(ImageSize original)
    {
        _original = original;
        _width = original.Width;
        _height = original.Height;
    }

    public override string Title => "Resize Image";

    public string OriginalSize => $"Currently {_original.Width} × {_original.Height}";

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private decimal? _width;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private decimal? _height;

    [ObservableProperty] private bool _keepAspectRatio = true;
    [ObservableProperty] private bool _smooth = true;

    partial void OnWidthChanged(decimal? value) =>
        Sync(() => Height = Math.Max(1, Math.Round((value ?? 0) * _original.Height / _original.Width)));

    partial void OnHeightChanged(decimal? value) =>
        Sync(() => Width = Math.Max(1, Math.Round((value ?? 0) * _original.Width / _original.Height)));

    private void Sync(Action update)
    {
        if (_syncing || !KeepAspectRatio) return;
        _syncing = true;
        try { update(); }
        finally { _syncing = false; }
    }

    protected override bool CanAccept() => Limits.IsValid(Width) && Limits.IsValid(Height);

    protected override ResizeRequest? CreateResult() => new((int)Width!.Value, (int)Height!.Value, Smooth);
}

public sealed partial class CanvasSizeViewModel(ImageSize current) : DialogViewModel<CanvasSizeRequest?>
{
    public override string Title => "Canvas Size";

    public string CurrentSize => $"Currently {current.Width} × {current.Height}";

    public static IReadOnlyList<Anchor> Anchors { get; } = Enum.GetValues<Anchor>();

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private decimal? _width = current.Width;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private decimal? _height = current.Height;

    [ObservableProperty] private Anchor _anchor = Anchor.Center;

    protected override bool CanAccept() => Limits.IsValid(Width) && Limits.IsValid(Height);

    protected override CanvasSizeRequest? CreateResult() => new((int)Width!.Value, (int)Height!.Value, Anchor);
}

/// <summary>An angle from a slider or typed in, shown on the canvas as it changes.</summary>
public sealed partial class RotateViewModel(Action<RotateRequest>? preview = null, bool selection = false)
    : DialogViewModel<RotateRequest?>
{
    public override string Title => selection ? "Rotate Selection" : "Rotate";

    public string ExpandText => selection ? "_Expand selection to fit" : "_Expand canvas to fit";

    /// <summary>In degrees, -180 to 180: positive turns clockwise, negative anticlockwise.</summary>
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private decimal? _angle = 0;

    [ObservableProperty] private bool _expand = true;

    /// <summary>The slider's side of <see cref="Angle" />, which can't take an empty value.</summary>
    public double SliderAngle
    {
        get => (double)(Angle ?? 0);
        set => Angle = Math.Round((decimal)value);
    }

    partial void OnAngleChanged(decimal? value)
    {
        OnPropertyChanged(nameof(SliderAngle));
        Preview();
    }

    partial void OnExpandChanged(bool value) => Preview();

    private void Preview()
    {
        if (CanAccept()) preview?.Invoke(CreateResult()!);
    }

    protected override bool CanAccept() => Angle is >= -180 and <= 180;

    protected override RotateRequest? CreateResult() => new((double)Angle!.Value, Expand);
}

/// <summary>The table of shortcuts behind Help → Keyboard.</summary>
public sealed class ShortcutsViewModel(IReadOnlyList<ShortcutGroup> groups) : DialogViewModel<bool?>
{
    public override string Title => "Keyboard";

    public override bool CanCancel => false;

    public IReadOnlyList<ShortcutGroup> Groups { get; } = groups;

    protected override bool? CreateResult() => true;
}

/// <summary>RGBA sliders and a hex box, kept in step with each other.</summary>
public sealed partial class ColorEditorViewModel : DialogViewModel<SKColor?>
{
    private bool _syncing;

    public ColorEditorViewModel(SKColor color)
    {
        _red = color.Red;
        _green = color.Green;
        _blue = color.Blue;
        _alpha = color.Alpha;
        _hex = ToHex(color);
    }

    public override string Title => "Edit Color";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    private int _red;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    private int _green;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    private int _blue;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Color))]
    private int _alpha;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _hex;

    public SKColor Color => new((byte)Red, (byte)Green, (byte)Blue, (byte)Alpha);

    partial void OnRedChanged(int value) => SyncHex();
    partial void OnGreenChanged(int value) => SyncHex();
    partial void OnBlueChanged(int value) => SyncHex();
    partial void OnAlphaChanged(int value) => SyncHex();

    partial void OnHexChanged(string value)
    {
        if (_syncing || !TryParseHex(value, out var color)) return;
        _syncing = true;
        try
        {
            Red = color.Red;
            Green = color.Green;
            Blue = color.Blue;
            Alpha = color.Alpha;
        }
        finally { _syncing = false; }
    }

    private void SyncHex()
    {
        if (_syncing) return;
        _syncing = true;
        try { Hex = ToHex(Color); }
        finally { _syncing = false; }
    }

    protected override bool CanAccept() => TryParseHex(Hex, out _);

    protected override SKColor? CreateResult() => Color;

    /// <summary>#RRGGBB when opaque, #AARRGGBB otherwise.</summary>
    public static string ToHex(SKColor color) => color.Alpha == 255
        ? $"#{color.Red:X2}{color.Green:X2}{color.Blue:X2}"
        : $"#{color.Alpha:X2}{color.Red:X2}{color.Green:X2}{color.Blue:X2}";

    public static bool TryParseHex(string? text, out SKColor color) =>
        SKColor.TryParse(text?.Trim() ?? "", out color);
}

public sealed partial class TextInputViewModel(string title) : DialogViewModel<string?>
{
    public override string Title { get; } = title;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(AcceptCommand))]
    private string _text = "";

    protected override bool CanAccept() => !string.IsNullOrEmpty(Text);

    protected override string? CreateResult() => Text;
}
