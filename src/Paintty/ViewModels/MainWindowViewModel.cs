using System.ComponentModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Paintty.Editing;
using Paintty.Imaging;
using Paintty.Services;
using SkiaSharp;

namespace Paintty.ViewModels;

public sealed partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly IImageCodec _codec;
    private readonly IDialogService _dialogs;
    private readonly IFileDialogService _files;
    private SKBitmap? _clipboard;

    public MainWindowViewModel(CanvasViewModel canvas, IImageCodec codec, IDialogService dialogs,
        IFileDialogService files)
    {
        Canvas = canvas;
        _codec = codec;
        _dialogs = dialogs;
        _files = files;

        canvas.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(CanvasViewModel.Document)) OnDocumentReplaced();
        };
        Watch(Document);
    }

    public CanvasViewModel Canvas { get; }
    public ToolboxViewModel Toolbox => Canvas.Toolbox;
    public PaletteViewModel Palette => Canvas.Palette;
    public Document Document => Canvas.Document;

    public string Title => $"{Document.DisplayName}{(Document.IsDirty ? " *" : "")} — Paintty";

    public string ImageSizeText => $"{Document.Width} × {Document.Height}";

    public string SelectionText => Document.Selection is { } s ? $"{s.Width} × {s.Height} at {s.Left}, {s.Top}" : "";

    /// <summary>The window should close; the user has already been asked about unsaved changes.</summary>
    public event Action? CloseRequested;

    // ── File ───────────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private async Task NewAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        if (await _dialogs.AskNewImageSizeAsync(new ImageSize(Document.Width, Document.Height)) is not { } size) return;
        ReplaceDocument(Document.CreateBlank(size.Width, size.Height, Palette.Secondary));
    }

    [RelayCommand]
    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardAsync()) return;
        if (await _files.PickImageToOpenAsync() is { } path)
            await OpenFileAsync(path);
    }

    /// <summary>Opens <paramref name="path" /> without asking about the current image.</summary>
    public async Task<bool> OpenFileAsync(string path)
    {
        try
        {
            var bitmap = await _codec.LoadAsync(Path.GetFullPath(path));
            ReplaceDocument(new Document(bitmap, Path.GetFullPath(path)));
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            await _dialogs.ShowErrorAsync("Open", ex.Message);
            return false;
        }
    }

    [RelayCommand]
    private async Task<bool> SaveAsync() =>
        Document.FilePath is { } path && _codec.WritableExtensions.Contains(Path.GetExtension(path).ToLowerInvariant())
            ? await SaveToAsync(path)
            : await SaveAsAsync();

    [RelayCommand]
    private async Task<bool> SaveAsAsync() =>
        await _files.PickImageToSaveAsync(Document.FilePath) is { } path && await SaveToAsync(path);

    private async Task<bool> SaveToAsync(string path)
    {
        Document.CommitFloating();
        // Encode a copy, so drawing can carry on while it is written out.
        using var snapshot = Document.Bitmap.Copy(Document.Bitmap.Bounds());
        try
        {
            await _codec.SaveAsync(snapshot, path);
            Document.MarkSaved(path);
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            await _dialogs.ShowErrorAsync("Save", ex.Message);
            return false;
        }
    }

    [RelayCommand]
    private async Task ExitAsync()
    {
        if (await ConfirmDiscardAsync())
            CloseRequested?.Invoke();
    }

    /// <summary>True when there is nothing unsaved, or the user saved it or chose to throw it away.</summary>
    public async Task<bool> ConfirmDiscardAsync()
    {
        if (!Document.IsDirty) return true;
        return await _dialogs.AskSaveChangesAsync(Document.DisplayName) switch
        {
            SaveChoice.Save => await SaveAsync(),
            SaveChoice.Discard => true,
            _ => false
        };
    }

    // ── Edit ───────────────────────────────────────────────────────────────────────────────

    private bool CanUndo() => Document.History.CanUndo || Document.HasFloating;
    private bool CanRedo() => Document.History.CanRedo;
    private bool HasSelection() => Document.Selection is not null;
    private bool HasClipboard() => _clipboard is not null;

    [RelayCommand(CanExecute = nameof(CanUndo))]
    private void Undo() => Document.Undo();

    [RelayCommand(CanExecute = nameof(CanRedo))]
    private void Redo() => Document.Redo();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Copy()
    {
        if (Document.CopySelection() is not { } pixels) return;
        _clipboard?.Dispose();
        _clipboard = pixels;
        PasteCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Cut()
    {
        Copy();
        Document.EraseSelection(Palette.Secondary);
        Document.Deselect();
    }

    /// <summary>Pastes as a floating selection at the top-left of the image, with the Select tool ready to move it.</summary>
    [RelayCommand(CanExecute = nameof(HasClipboard))]
    private void Paste()
    {
        if (_clipboard is null) return;
        Toolbox.SelectedTool = Toolbox.Get<Tools.SelectTool>();
        Document.PasteFloating(_clipboard.Copy(_clipboard.Bounds()), 0, 0);
    }

    [RelayCommand]
    private void SelectAll()
    {
        Toolbox.SelectedTool = Toolbox.Get<Tools.SelectTool>();
        Document.SelectAll();
    }

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void Deselect() => Document.Deselect();

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void EraseSelection() => Document.EraseSelection(Palette.Secondary);

    /// <summary>Esc: cancel the tool's gesture, or put down a floating selection.</summary>
    [RelayCommand]
    private void Escape()
    {
        Canvas.CancelTool();
        Document.CommitFloating();
    }

    // ── Image ──────────────────────────────────────────────────────────────────────────────

    [RelayCommand(CanExecute = nameof(HasSelection))]
    private void CropToSelection() => Document.CropToSelection();

    [RelayCommand]
    private async Task ResizeAsync()
    {
        if (await _dialogs.AskResizeAsync(new ImageSize(Document.Width, Document.Height)) is not { } request) return;
        Document.ApplyToImage("Resize", bitmap => Adjustments.Resize(bitmap, request.Width, request.Height, request.Smooth));
    }

    [RelayCommand]
    private async Task CanvasSizeAsync()
    {
        if (await _dialogs.AskCanvasSizeAsync(new ImageSize(Document.Width, Document.Height)) is not { } request) return;
        Document.ApplyToImage("Canvas Size", bitmap =>
            Adjustments.CanvasSize(bitmap, request.Width, request.Height, request.Anchor, Palette.Secondary));
    }

    [RelayCommand]
    private void FlipHorizontal() =>
        Document.ApplyToSelectionOrImage("Flip Horizontal", bitmap => Adjustments.Mirror(bitmap, Flip.Horizontal));

    [RelayCommand]
    private void FlipVertical() =>
        Document.ApplyToSelectionOrImage("Flip Vertical", bitmap => Adjustments.Mirror(bitmap, Flip.Vertical));

    [RelayCommand]
    private void Rotate(string degrees) =>
        Document.ApplyToImage($"Rotate {degrees}°", bitmap => Adjustments.Rotate(bitmap, int.Parse(degrees)));

    // ── Adjustments ────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private void Invert() => Document.ApplyToSelectionOrImage("Invert Colors", Adjustments.Invert);

    [RelayCommand]
    private void Grayscale() => Document.ApplyToSelectionOrImage("Black and White", Adjustments.Grayscale);

    // ── Help ───────────────────────────────────────────────────────────────────────────────

    [RelayCommand]
    private Task AboutAsync() => _dialogs.ShowMessageAsync("About Paintty",
        "Paintty — a paint program for the terminal.\n\n" +
        "Left button draws with the primary colour, right button with the secondary.\n" +
        "Tools: " + string.Join(", ", Toolbox.Tools.Select(tool => $"{tool.Name} ({tool.Hotkey})")) + ".\n" +
        "X swaps the colours.\n\n" +
        $"Graphics: {Canvas.DisplayMetrics.Graphics}.");

    // ── plumbing ───────────────────────────────────────────────────────────────────────────

    private void ReplaceDocument(Document document)
    {
        var old = Document;
        Canvas.Document = document;
        old.Dispose();
        Canvas.ZoomToFitCommand.Execute(null);
    }

    private void OnDocumentReplaced()
    {
        Watch(Document);
        Refresh();
    }

    private void Watch(Document document)
    {
        document.PropertyChanged += OnDocumentPropertyChanged;
        document.History.PropertyChanged += (_, _) => Refresh();
    }

    private void OnDocumentPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender != Document) return;
        if (e.PropertyName != nameof(Editing.Document.Revision)) Refresh();
    }

    private void Refresh()
    {
        OnPropertyChanged(nameof(Document));
        OnPropertyChanged(nameof(Title));
        OnPropertyChanged(nameof(ImageSizeText));
        OnPropertyChanged(nameof(SelectionText));
        UndoCommand.NotifyCanExecuteChanged();
        RedoCommand.NotifyCanExecuteChanged();
        CopyCommand.NotifyCanExecuteChanged();
        CutCommand.NotifyCanExecuteChanged();
        DeselectCommand.NotifyCanExecuteChanged();
        EraseSelectionCommand.NotifyCanExecuteChanged();
        CropToSelectionCommand.NotifyCanExecuteChanged();
    }

    public void Dispose()
    {
        _clipboard?.Dispose();
        Canvas.Dispose();
        Document.Dispose();
    }
}
