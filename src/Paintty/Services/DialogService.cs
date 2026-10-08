using Consolonia.Controls;
using Paintty.ViewModels.Dialogs;
using Paintty.Views.Dialogs;
using SkiaSharp;

namespace Paintty.Services;

/// <summary>Shows the editor's dialogs as Consolonia managed windows.</summary>
public sealed class DialogService : IDialogService
{
    public async Task<SaveChoice> AskSaveChangesAsync(string documentName) =>
        await MessageBox.ShowDialog("Paintty", $"Save changes to {documentName}?", MessageBoxStyle.YesNoCancel) switch
        {
            MessageBoxResult.Yes => SaveChoice.Save,
            MessageBoxResult.No => SaveChoice.Discard,
            _ => SaveChoice.Cancel
        };

    public Task ShowErrorAsync(string title, string message) => MessageBox.ShowDialog(title, message);

    public Task ShowMessageAsync(string title, string message) => MessageBox.ShowDialog(title, message);

    public Task<ImageSize?> AskNewImageSizeAsync(ImageSize suggested) => ShowAsync(new NewImageViewModel(suggested));

    public Task<ResizeRequest?> AskResizeAsync(ImageSize current) => ShowAsync(new ResizeViewModel(current));

    public Task<CanvasSizeRequest?> AskCanvasSizeAsync(ImageSize current) => ShowAsync(new CanvasSizeViewModel(current));

    public Task<SKColor?> EditColorAsync(SKColor color) => ShowAsync(new ColorEditorViewModel(color));

    public Task<string?> PromptTextAsync(string title) => ShowAsync(new TextInputViewModel(title));

    private static async Task<TResult> ShowAsync<TResult>(DialogViewModel<TResult> viewModel)
    {
        var window = new DialogWindow { DataContext = viewModel };
        viewModel.CloseRequested += result => window.Close(result);
        object? result = await window.ShowDialog<object?>();
        return result is TResult typed ? typed : default!;
    }
}
