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
    ///     Single letters pick tools (and X swaps colours), the way Paint.NET does — but only when no
    ///     text box has the focus, so typing a font name doesn't switch tools. Esc is here rather than
    ///     a key binding so that an open dialog, which handles Esc itself, gets it first.
    /// </summary>
    private void OnToolHotkey(object? sender, KeyEventArgs e)
    {
        if (e.Handled || e.KeyModifiers != KeyModifiers.None || ViewModel is not { } viewModel) return;
        if (e.Source is Visual source && source.GetSelfAndVisualAncestors().OfType<ManagedWindow>().Any()) return;
        if (e.Key == Key.Escape)
        {
            viewModel.EscapeCommand.Execute(null);
            e.Handled = true;
            return;
        }

        if (FocusManager?.GetFocusedElement() is TextBox or NumericUpDown or ComboBox) return;
        if (e.Key is < Key.A or > Key.Z) return;

        char letter = (char)('A' + (e.Key - Key.A));
        if (letter == 'X')
            viewModel.Palette.SwapCommand.Execute(null);
        else if (!viewModel.Toolbox.TrySelect(letter))
            return;
        e.Handled = true;
    }
}
