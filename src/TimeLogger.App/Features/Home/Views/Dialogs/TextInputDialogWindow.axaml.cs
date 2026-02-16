using Avalonia.Controls;

namespace TimeLogger.App.Features.Home.Views.Dialogs;

public partial class TextInputDialogWindow : Window
{
    public TextInputDialogWindow()
    {
        InitializeComponent();
    }

    public TextInputDialogWindow(string title, string prompt, string watermark, string initialValue = "")
        : this()
    {
        Title = title;
        PromptTextBlock.Text = prompt;
        InputTextBox.Watermark = watermark;
        InputTextBox.Text = initialValue;
    }

    private void OnOkClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var value = InputTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(value))
        {
            return;
        }

        Close(value);
    }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
