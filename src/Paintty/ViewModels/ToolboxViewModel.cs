using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Paintty.Tools;

namespace Paintty.ViewModels;

public sealed partial class ToolboxViewModel : ObservableObject
{
    public ToolboxViewModel(IEnumerable<ToolBase> tools)
    {
        Tools = [.. tools];
        _selectedTool = Tools.OfType<PencilTool>().FirstOrDefault() ?? Tools[0];
    }

    public IReadOnlyList<ToolBase> Tools { get; }

    [ObservableProperty] private ToolBase _selectedTool;

    /// <summary>Raised before the selection moves on, so the outgoing tool can be finished off.</summary>
    public event Action<ToolBase>? Deselecting;

    partial void OnSelectedToolChanging(ToolBase value) => Deselecting?.Invoke(SelectedTool);

    public T Get<T>() where T : ToolBase => Tools.OfType<T>().Single();

    /// <summary>Selects the tool bound to <paramref name="key" />; false when no tool is.</summary>
    public bool TrySelect(char key)
    {
        var tool = Tools.FirstOrDefault(t => char.ToUpperInvariant(t.Hotkey) == char.ToUpperInvariant(key));
        if (tool is null) return false;
        SelectedTool = tool;
        return true;
    }

    /// <summary>Gives the selected tool a typed digit as its stroke width; false when it has none.</summary>
    public bool TrySetWidth(int width) => SelectedTool.TrySetWidth(width);

    [RelayCommand]
    private void Select(ToolBase tool) => SelectedTool = tool;
}
