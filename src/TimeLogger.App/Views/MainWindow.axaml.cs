using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Styling;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.Features.Home.Views.Dialogs;
using TimeLogger.App.ViewModels;

namespace TimeLogger.App.Views;

public partial class MainWindow : Window
{
    private readonly AppSettingsStore _store = new();
    private AppSettings _settings = new();
    private string? _loadError;

    public MainWindow()
    {
        InitializeComponent();
        try { _settings = _store.Load(); }
        catch (Exception ex) { _loadError = ex.Message; }
        ApplyTheme(_settings.Theme);
        DataContextChanged += (_, _) => ApplyTasks();
        Opened += async (_, _) =>
        {
            if (_loadError is not null)
                await ShowErrorAsync("Settings could not be loaded", $"Your settings were not changed. { _loadError }");
        };
    }

    private void ApplyTasks()
    {
        if (DataContext is MainWindowViewModel vm) vm.Home.SetTaskTitles(_settings.TaskTitles);
    }

    private void ApplyTheme(string theme)
    {
        if (Application.Current is not { } app) return;
        app.RequestedThemeVariant = theme switch { "Light" => ThemeVariant.Light, "System" => ThemeVariant.Default, _ => ThemeVariant.Dark };
        if (theme == "Light") ThemeButton.Content = "☾  Dark mode";
        else if (theme == "Dark") ThemeButton.Content = "☀  Light mode";
        else Avalonia.Threading.Dispatcher.UIThread.Post(UpdateThemeButton);
    }

    private void UpdateThemeButton() => ThemeButton.Content = Application.Current?.ActualThemeVariant == ThemeVariant.Dark
        ? "☀  Light mode" : "☾  Dark mode";

    private async void OnThemeToggleClicked(object? sender, RoutedEventArgs e)
    {
        if (_loadError is not null) { await ShowErrorAsync("Settings unavailable", "The settings file could not be read, so it will not be overwritten."); return; }
        var next = new AppSettings { TaskTitles = new(_settings.TaskTitles), Theme = Application.Current?.ActualThemeVariant == ThemeVariant.Dark ? "Light" : "Dark" };
        try { _store.Save(next); _settings = next; ApplyTheme(next.Theme); }
        catch (Exception ex) { await ShowErrorAsync("Could not save settings", ex.Message); }
    }

    private async void OnSettingsClicked(object? sender, RoutedEventArgs e)
    {
        if (_loadError is not null) { await ShowErrorAsync("Settings unavailable", "The settings file could not be read, so it will not be overwritten."); return; }
        var updated = await new SettingsWindow(_settings).ShowDialog<AppSettings?>(this);
        if (updated is null) return;
        try { _store.Save(updated); _settings = updated; ApplyTasks(); ApplyTheme(updated.Theme); }
        catch (Exception ex) { await ShowErrorAsync("Could not save settings", ex.Message); }
    }

    private async System.Threading.Tasks.Task ShowErrorAsync(string title, string message) =>
        await new MessageDialogWindow(title, message, "OK", null).ShowDialog<bool>(this);
}
