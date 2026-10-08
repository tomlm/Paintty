using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using Avalonia.VisualTree;
using Iciclecreek.Avalonia.WindowManager;
using Paintty.ViewModels.Dialogs;

namespace Paintty.Views.Dialogs;

/// <summary>Hosts any dialog view model, picking its body from the window's data templates.</summary>
public partial class DialogWindow : ManagedWindow
{
    public DialogWindow()
    {
        AvaloniaXamlLoader.Load(this);
        // The body comes from a data template, so it only exists once the window has been laid out.
        Opened += (_, _) => Dispatcher.UIThread.Post(FocusFirstInput, DispatcherPriority.Loaded);
        AddHandler(KeyDownEvent, OnDialogKey, RoutingStrategies.Tunnel, handledEventsToo: true);
    }

    /// <summary>
    ///     ManagedWindow answers Enter and Esc by raising Click on the default and cancel buttons, which
    ///     doesn't run a bound Command, so run the dialog's commands here.
    /// </summary>
    private void OnDialogKey(object? sender, KeyEventArgs e)
    {
        if (DataContext is not IDialogViewModel dialog || e.KeyModifiers != KeyModifiers.None) return;
        var command = e.Key switch
        {
            Key.Escape => dialog.CancelCommand,
            Key.Enter => dialog.AcceptCommand,
            _ => null
        };
        if (command is null || !command.CanExecute(null)) return;

        // ManagedWindow has already marked Enter handled, so a NumericUpDown never sees it and keeps
        // typed-in text uncommitted. Moving focus off commits it; run the command once that has landed.
        this.FindControl<Button>("AcceptButton")?.Focus();
        Dispatcher.UIThread.Post(() =>
        {
            if (command.CanExecute(null)) command.Execute(null);
        }, DispatcherPriority.Input);
        e.Handled = true;
    }

    /// <summary>
    ///     Focus has to be inside the window for its Enter and Esc handling to see the keys — and in the
    ///     body, not the title bar, whose menu would swallow them.
    /// </summary>
    private void FocusFirstInput()
    {
        var body = this.FindControl<ContentControl>("Body")!;
        var first = body.GetVisualDescendants().OfType<InputElement>()
            .FirstOrDefault(element => element.Focusable && element.IsEffectivelyEnabled && element.IsEffectivelyVisible);
        (first ?? this.FindControl<Button>("AcceptButton"))?.Focus();
    }
}
