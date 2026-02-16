using Avalonia.Controls;

namespace TimeLogger.App.Features.Home.Views.Dialogs;

public partial class MessageDialogWindow : Window
{
    public MessageDialogWindow()
    {
        InitializeComponent();
    }

    public MessageDialogWindow(string title, string message, string confirmText, string? cancelText)
        : this()
    {
        Title = title;
        MessageTextBlock.Text = message;
        ConfirmButton.Content = confirmText;

        if (string.IsNullOrWhiteSpace(cancelText))
        {
            CancelButton.IsVisible = false;
        }
        else
        {
            CancelButton.Content = cancelText;
        }
    }

    private void OnConfirmClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(true);
    }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(false);
    }
}
