using Avalonia.Controls;

namespace TimeLogger.App.Features.Home.Views.Dialogs;

public partial class CustomTaskDialogWindow : Window
{
    public CustomTaskDialogWindow()
    {
        InitializeComponent();
    }

    private void OnOkClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var taskName = TaskNameTextBox.Text?.Trim();
        if (string.IsNullOrWhiteSpace(taskName))
        {
            return;
        }

        Close(taskName);
    }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }
}
