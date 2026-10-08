using Paintty.Imaging;
using SkiaSharp;

namespace Paintty.Editing;

/// <summary>One undoable change to a <see cref="Document" />.</summary>
public interface IEdit : IDisposable
{
    string Name { get; }
    void Undo(Document document);
    void Redo(Document document);
}

/// <summary>A change confined to a rectangle: keeps only the pixels it covered, before and after.</summary>
public sealed class RegionEdit(string name, SKRectI region, SKBitmap before, SKBitmap after) : IEdit
{
    public string Name { get; } = name;
    public SKRectI Region { get; } = region;

    public void Undo(Document document) => document.WritePixels(before, Region);
    public void Redo(Document document) => document.WritePixels(after, Region);

    public void Dispose()
    {
        before.Dispose();
        after.Dispose();
    }
}

/// <summary>A change to the whole image, such as a resize or rotation, that may change its size.</summary>
public sealed class BitmapEdit(string name, SKBitmap before, SKBitmap after) : IEdit
{
    public string Name { get; } = name;

    public void Undo(Document document) => document.SwapBitmap(before.Copy());
    public void Redo(Document document) => document.SwapBitmap(after.Copy());

    public void Dispose()
    {
        before.Dispose();
        after.Dispose();
    }
}

/// <summary>
///     An edit in progress. Snapshots the image when it opens; the tool reports each rectangle it
///     touches, and <see cref="Commit" /> records just the union of them as a <see cref="RegionEdit" />.
/// </summary>
public sealed class EditScope : IDisposable
{
    private readonly Document _document;
    private SKBitmap? _before;
    private SKRectI _dirty = SKRectI.Empty;

    internal EditScope(Document document, string name)
    {
        _document = document;
        Name = name;
        _before = document.Bitmap.Copy();
    }

    public string Name { get; }
    public Document Document => _document;
    public bool IsOpen => _before is not null;
    public SKRectI Dirty => _dirty;

    /// <summary>Records that pixels inside <paramref name="rect" /> may have changed, and redraws.</summary>
    public void Touch(SKRectI rect)
    {
        rect = _document.Bitmap.Clip(rect);
        if (rect.IsEmpty) return;
        _dirty = _dirty.IsEmpty ? rect : SKRectI.Union(_dirty, rect);
        _document.NotifyPixelsChanged();
    }

    /// <summary>Touches the bounds of a float rectangle, widened by <paramref name="inflate" /> on each side.</summary>
    public void Touch(SKRect rect, float inflate)
    {
        rect.Inflate(inflate + 1, inflate + 1);
        Touch(SKRectI.Round(rect.Standardized));
    }

    /// <summary>Puts the snapshot back, as if the edit never happened.</summary>
    public void Revert()
    {
        if (_before is null) return;
        if (!_dirty.IsEmpty)
        {
            using var original = _before.Copy(_dirty);
            _document.WritePixels(original, _dirty);
        }

        Close();
    }

    public void Commit()
    {
        if (_before is null) return;
        if (!_dirty.IsEmpty)
            _document.History.Push(new RegionEdit(Name, _dirty, _before.Copy(_dirty), _document.Bitmap.Copy(_dirty)));
        Close();
    }

    public void Dispose() => Commit();

    private void Close()
    {
        _before?.Dispose();
        _before = null;
        _document.EndEdit(this);
    }
}
