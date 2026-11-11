using System;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeLogger.App.Features.Home.Services;

namespace TimeLogger.App.Features.Home.Views.Dialogs;

public partial class SettingsWindow : Window
{
    private readonly ObservableCollection<string> _tasks = new();
    public SettingsWindow() { InitializeComponent(); TasksList.ItemsSource = _tasks; }
    public SettingsWindow(AppSettings settings) : this()
    {
        foreach (var task in settings.TaskTitles) _tasks.Add(task);
        ThemePicker.SelectedIndex = settings.Theme switch { "Light" => 1, "System" => 2, _ => 0 };
    }

    private void OnAddClicked(object? sender, RoutedEventArgs e)
    {
        var title = NewTaskBox.Text?.Trim() ?? "";
        if (title.Length == 0) { ShowError("Enter a task title."); return; }
        if (title.Equals("Other", StringComparison.OrdinalIgnoreCase)) { ShowError("Other is reserved for one-off tasks."); return; }
        if (_tasks.Any(x => x.Equals(title, StringComparison.OrdinalIgnoreCase))) { ShowError("That task title is already in the list."); return; }
        _tasks.Add(title);
        NewTaskBox.Text = "";
        ErrorText.IsVisible = false;
    }
    private void OnRemoveClicked(object? sender, RoutedEventArgs e)
    {
        if (TasksList.SelectedItem is string selected) { _tasks.Remove(selected); ErrorText.IsVisible = false; }
        else ShowError("Select a task title to remove.");
    }
    private void OnSaveClicked(object? sender, RoutedEventArgs e) => Close(new AppSettings
    {
        TaskTitles = _tasks.ToList(),
        Theme = ThemePicker.SelectedIndex switch { 1 => "Light", 2 => "System", _ => "Dark" }
    });
    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close(null);
    private void ShowError(string message) { ErrorText.Text = message; ErrorText.IsVisible = true; }
}
