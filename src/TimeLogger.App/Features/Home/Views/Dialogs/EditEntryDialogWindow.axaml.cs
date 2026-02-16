using System.Collections.Generic;
using Avalonia.Controls;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Views.Dialogs;

public partial class EditEntryDialogWindow : Window
{
    public EditEntryDialogWindow()
    {
        InitializeComponent();
    }

    public EditEntryDialogWindow(WorkEntry entry, IReadOnlyList<string> timeOptions)
        : this()
    {
        StartTimeComboBox.ItemsSource = timeOptions;
        EndTimeComboBox.ItemsSource = timeOptions;

        TaskTextBox.Text = entry.Task;
        NotesTextBox.Text = entry.Notes;
        StartTimeComboBox.SelectedItem = FormatTime(entry.Start);
        EndTimeComboBox.SelectedItem = FormatTime(entry.End);
    }

    private void OnSaveClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        var taskName = TaskTextBox.Text?.Trim();
        var notes = NotesTextBox.Text?.Trim();
        var startTime = StartTimeComboBox.SelectedItem as string;
        var endTime = EndTimeComboBox.SelectedItem as string;

        if (string.IsNullOrWhiteSpace(taskName) ||
            string.IsNullOrWhiteSpace(notes) ||
            string.IsNullOrWhiteSpace(startTime) ||
            string.IsNullOrWhiteSpace(endTime))
        {
            return;
        }

        Close(new EditEntryInput
        {
            TaskName = taskName,
            Notes = notes,
            StartTime = startTime,
            EndTime = endTime
        });
    }

    private void OnCancelClicked(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }

    private static string FormatTime(System.TimeSpan time)
    {
        return System.DateTime.Today.Add(time).ToString("hh:mm tt");
    }
}
