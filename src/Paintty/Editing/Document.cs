using CommunityToolkit.Mvvm.ComponentModel;
using Paintty.Imaging;
using SkiaSharp;

namespace Paintty.Editing;

/// <summary>
///     The image being edited: one <see cref="SKBitmap" />, its undo history, and the selection.
///     A selection can be <em>floating</em> — pixels lifted off the image (by moving or pasting) that
///     sit above it until they are committed back down.
/// </summary>
public sealed partial class Document : ObservableObject, IDisposable
{
    private SKBitmap _bitmap;
    private EditScope? _activeEdit;
    private EditScope? _floatingEdit;

    public Document(SKBitmap bitmap, string? filePath = null)
    {
        _bitmap = Bitmaps.Normalize(bitmap);
        _filePath = filePath;
        History.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(Editing.History.DistanceFromSave))
                OnPropertyChanged(nameof(IsDirty));
        };
    }

    public static Document CreateBlank(int width, int height, SKColor background) =>
        new(Bitmaps.Create(width, height, background));

    public SKBitmap Bitmap => _bitmap;
    public int Width => _bitmap.Width;
    public int Height => _bitmap.Height;

    public History History { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(DisplayName))]
    private string? _filePath;

    public string DisplayName => FilePath is null ? "Untitled" : Path.GetFileName(FilePath);

    public bool IsDirty => History.DistanceFromSave != 0;

    /// <summary>Bumped whenever anything that is drawn changes: pixels, selection or floating pixels.</summary>
    [ObservableProperty] private int _revision;

    /// <summary>The selected rectangle, in image pixels; null when nothing is selected.</summary>
    [ObservableProperty] private SKRectI? _selection;

    /// <summary>Pixels lifted off the image, drawn at <see cref="Selection" />'s top-left.</summary>
    public SKBitmap? Floating { get; private set; }

    public bool HasFloating => Floating is not null;

    partial void OnSelectionChanged(SKRectI? value) => Revision++;

    // ── edits ──────────────────────────────────────────────────────────────────────────────

    /// <summary>
    ///     Starts an undoable change. Draw on <see cref="Bitmap" />, <see cref="EditScope.Touch(SKRectI)" />
    ///     what changed, then commit (or dispose) the scope.
    /// </summary>
    public EditScope BeginEdit(string name)
    {
        CommitFloating();
        _activeEdit?.Commit();
        return _activeEdit = new EditScope(this, name);
    }

    internal void EndEdit(EditScope scope)
    {
        if (_activeEdit == scope) _activeEdit = null;
        if (_floatingEdit == scope) _floatingEdit = null;
    }

    /// <summary>Replaces the whole image with what <paramref name="transform" /> makes of it.</summary>
    public void ApplyToImage(string name, Func<SKBitmap, SKBitmap> transform)
    {
        Deselect();
        var before = _bitmap;
        var after = transform(before);
        if (ReferenceEquals(after, before)) return;

        History.Push(new BitmapEdit(name, before, after.Copy()));
        SetBitmap(after, disposeOld: false);
    }

    /// <summary>
    ///     Applies a same-size transform to the selection when there is one, otherwise to the whole
    ///     image — the way Paint.NET's adjustments behave.
    /// </summary>
    public void ApplyToSelectionOrImage(string name, Func<SKBitmap, SKBitmap> transform)
    {
        if (Floating is { } floating)
        {
            // Transform the lifted pixels in place; they are committed with the rest of the move.
            var result = transform(floating);
            floating.Dispose();
            Floating = result;
            Revision++;
            return;
        }

        if (Selection is not { } region)
        {
            ApplyToImage(name, transform);
            return;
        }

        using var scope = BeginEdit(name);
        using var part = _bitmap.Copy(region);
        using var changed = transform(part);
        _bitmap.Replace(changed, region.Left, region.Top);
        scope.Touch(region);
    }

    public void Undo()
    {
        CommitFloating();
        History.Undo(this);
    }

    public void Redo()
    {
        CommitFloating();
        History.Redo(this);
    }

    internal void WritePixels(SKBitmap pixels, SKRectI region, bool notify = true)
    {
        _bitmap.Replace(pixels, region.Left, region.Top);
        if (notify) NotifyPixelsChanged();
    }

    internal void SwapBitmap(SKBitmap bitmap)
    {
        Selection = null;
        SetBitmap(bitmap, disposeOld: true);
    }

    public void NotifyPixelsChanged()
    {
        _bitmap.NotifyPixelsChanged();
        Revision++;
    }

    public void MarkSaved(string path)
    {
        FilePath = path;
        History.MarkSaved();
    }

    private void SetBitmap(SKBitmap bitmap, bool disposeOld)
    {
        var old = _bitmap;
        _bitmap = bitmap;
        if (disposeOld) old.Dispose();
        OnPropertyChanged(nameof(Bitmap));
        OnPropertyChanged(nameof(Width));
        OnPropertyChanged(nameof(Height));
        Revision++;
    }

    // ── selection ──────────────────────────────────────────────────────────────────────────

    public void Select(SKRectI rect)
    {
        CommitFloating();
        rect = _bitmap.Clip(rect.Standardized);
        Selection = rect.IsEmpty ? null : rect;
    }

    public void SelectAll() => Select(_bitmap.Bounds());

    public void Deselect()
    {
        CommitFloating();
        Selection = null;
    }

    /// <summary>Lifts the selected pixels so they can be moved, leaving <paramref name="hole" /> behind.</summary>
    public void LiftSelection(SKColor hole)
    {
        if (Floating is not null || Selection is not { } region) return;

        var scope = BeginEdit("Move Selection");
        Floating = _bitmap.Copy(region);
        _bitmap.FillRect(region, hole);
        scope.Touch(region);
        _floatingEdit = scope;
        _activeEdit = null;
        OnPropertyChanged(nameof(HasFloating));
    }

    /// <summary>Puts <paramref name="pixels" /> above the image at (x, y) as a floating selection it then owns.</summary>
    public void PasteFloating(SKBitmap pixels, int x, int y)
    {
        CommitFloating();
        _activeEdit?.Commit();
        _floatingEdit = new EditScope(this, "Paste");
        Floating = Bitmaps.Normalize(pixels);
        Selection = new SKRectI(x, y, x + pixels.Width, y + pixels.Height);
        OnPropertyChanged(nameof(HasFloating));
    }

    public void MoveFloating(int x, int y)
    {
        if (Floating is null || Selection is not { } region || (region.Left == x && region.Top == y)) return;
        Selection = new SKRectI(x, y, x + region.Width, y + region.Height);
    }

    /// <summary>Draws the floating pixels down onto the image and closes the move or paste.</summary>
    public void CommitFloating()
    {
        if (Floating is not { } floating || Selection is not { } region) return;

        _bitmap.Composite(floating, region.Left, region.Top);
        Floating = null;
        floating.Dispose();
        _floatingEdit?.Touch(region);
        _floatingEdit?.Commit();
        Selection = _bitmap.Clip(region) is { IsEmpty: false } onImage ? onImage : null;
        OnPropertyChanged(nameof(HasFloating));
    }

    /// <summary>Drops the floating pixels without drawing them; whatever hole the lift left stays.</summary>
    public void DiscardFloating()
    {
        if (Floating is not { } floating) return;
        Floating = null;
        floating.Dispose();
        _floatingEdit?.Commit();
        Selection = null;
        OnPropertyChanged(nameof(HasFloating));
    }

    /// <summary>The selected pixels (floating or not), as a new bitmap; null without a selection.</summary>
    public SKBitmap? CopySelection()
    {
        if (Floating is { } floating) return floating.Copy(floating.Bounds());
        return Selection is { } region ? _bitmap.Copy(region) : null;
    }

    /// <summary>Fills the selection with <paramref name="color" />, or drops floating pixels.</summary>
    public void EraseSelection(SKColor color)
    {
        if (Floating is not null)
        {
            DiscardFloating();
            return;
        }

        if (Selection is not { } region) return;
        using var scope = BeginEdit("Erase Selection");
        _bitmap.FillRect(region, color);
        scope.Touch(region);
    }

    public void CropToSelection()
    {
        CommitFloating();
        if (Selection is not { } region) return;
        ApplyToImage("Crop to Selection", bitmap => bitmap.Copy(region));
    }

    public void Dispose()
    {
        Floating?.Dispose();
        History.Dispose();
        _bitmap.Dispose();
    }
}
