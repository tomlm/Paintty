using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Markup.Xaml;
using Avalonia.VisualTree;
using Iciclecreek.Avalonia.WindowManager;
using Paintty.ViewModels;

namespace Paintty.Views;

public partial class MainWindow : Window
{
    private bool _closeConfirmed;

    public MainWindow()
    {
        AvaloniaXamlLoader.Load(this);
        DataContextChanged += (_, _) =>
        {
            if (ViewModel is { } viewModel)
                viewModel.CloseRequested += () =>
                {
                    _closeConfirmed = true;
                    Close();
                };
        };
        Closing += OnClosing;
        AddHandler(KeyDownEvent, OnToolHotkey, Avalonia.Interactivity.RoutingStrategies.Bubble);
        AddHandler(KeyDownEvent, OnPanKey, Avalonia.Interactivity.RoutingStrategies.Tunnel);
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    /// <summary>Ask about unsaved changes however the window is being closed.</summary>
    private async void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (_closeConfirmed || ViewModel is not { Document.IsDirty: true } viewModel) return;

        e.Cancel = true;
        if (await viewModel.ConfirmDiscardAsync())
        {
            _closeConfirmed = true;
            Close();
        }
    }

    /// <summary>
    ///     The arrow keys pan the picture. They are taken on the way down, before the focused control
    ///     sees them: focus usually rests on the menu bar, which would otherwise take them. An open
    ///     menu, a dialog, or a control that edits with arrows keeps them.
    /// </summary>
    private void OnPanKey(object? sender, KeyEventArgs e)
    {
        if (e.Key is not (Key.Left or Key.Right or Key.Up or Key.Down) || e.KeyModifiers != KeyModifiers.None) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<ManagedWindow>().Any()) return;
        if (this.GetVisualDescendants().OfType<Menu>().Any(menu => menu.IsOpen)) return;
        if (FocusManager?.GetFocusedElement() is TextBox or NumericUpDown or ComboBox or Slider) return;

        this.FindControl<CanvasView>("CanvasView")?.PanByArrow(e.Key);
        e.Handled = true;
    }

    /// <summary>
    ///     Single letters pick tools (and X swaps colours), the way Paint.NET does, and + and - zoom in
    ///     and out — but only when no text box has the focus, so typing a font name doesn't switch tools.
    ///     The zoom keys are plain keys because terminals keep the Ctrl combinations for themselves. Esc is here rather than
    ///     a key binding so that an open dialog, which handles Esc itself, gets it first.
    /// </summary>
    private void OnToolHotkey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || ViewModel is not { } viewModel) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<ManagedWindow>().Any()) return;
        if (e.Key == Key.Escape && e.KeyModifiers == KeyModifiers.None)
        {
            viewModel.EscapeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (FocusManager?.GetFocusedElement() is TextBox or NumericUpDown or ComboBox) return;
        if (ZoomKey(e) is { } zoom)
        {
            zoom.Execute(null);
            e.Handled = true;
            return;
        }

        if (e.KeyModifiers != KeyModifiers.None || e.Key is < Key.A or > Key.Z) return;

        char letter = (char)('A' + (e.Key - Key.A));
        if (letter == 'X')
            viewModel.Palette.SwapCommand.Execute(null);
        else if (!viewModel.Toolbox.TrySelect(letter))
            return;
        e.Handled = true;
    }

    /// <summary>
    ///     The zoom command for a typed + or -, by the character, never the key: + is Shift+= on many
    ///     layouts and a key of its own on others, and the keypad has both. Shift is allowed, for the
    ///     layouts that need it; Ctrl and Alt are not.
    /// </summary>
    private System.Windows.Input.ICommand? ZoomKey(KeyEventArgs e)
    {
        if ((e.KeyModifiers & ~KeyModifiers.Shift) != KeyModifiers.None) return null;
        return e.KeySymbol switch
        {
            "+" => ViewModel!.Canvas.ZoomInCommand,
            "-" => ViewModel!.Canvas.ZoomOutCommand,
            _ => null
        };
    }
}
