using CommunityToolkit.Mvvm.ComponentModel;

namespace Paintty.Editing;

/// <summary>Undo and redo stacks. Pushing a new edit throws away whatever could have been redone.</summary>
public sealed partial class History : ObservableObject, IDisposable
{
    private readonly LinkedList<IEdit> _undo = new();
    private readonly Stack<IEdit> _redo = new();

    public History(int capacity = 100) => Capacity = capacity;

    public int Capacity { get; }

    /// <summary>How many edits since the document was last saved; 0 means the image matches the file.</summary>
    [ObservableProperty] private int _distanceFromSave;

    public bool CanUndo => _undo.Count > 0;
    public bool CanRedo => _redo.Count > 0;
    public string? UndoName => _undo.Last?.Value.Name;
    public string? RedoName => _redo.TryPeek(out var edit) ? edit.Name : null;

    public void Push(IEdit edit)
    {
        foreach (var redo in _redo) redo.Dispose();
        _redo.Clear();

        _undo.AddLast(edit);
        while (_undo.Count > Capacity)
        {
            _undo.First!.Value.Dispose();
            _undo.RemoveFirst();
        }

        // An edit made after undoing past the save point can never get back to it.
        DistanceFromSave = DistanceFromSave < 0 ? int.MaxValue : DistanceFromSave + 1;
        Changed();
    }

    public void Undo(Document document)
    {
        if (_undo.Last is not { } node) return;
        _undo.RemoveLast();
        node.Value.Undo(document);
        _redo.Push(node.Value);
        DistanceFromSave--;
        Changed();
    }

    public void Redo(Document document)
    {
        if (!_redo.TryPop(out var edit)) return;
        edit.Redo(document);
        _undo.AddLast(edit);
        DistanceFromSave++;
        Changed();
    }

    public void MarkSaved() => DistanceFromSave = 0;

    public void Dispose()
    {
        foreach (var edit in _undo.Concat(_redo)) edit.Dispose();
        _undo.Clear();
        _redo.Clear();
    }

    private void Changed()
    {
        OnPropertyChanged(nameof(CanUndo));
        OnPropertyChanged(nameof(CanRedo));
        OnPropertyChanged(nameof(UndoName));
        OnPropertyChanged(nameof(RedoName));
    }
}
