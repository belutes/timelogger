using System;
using System.Linq;
using System.Collections.Generic;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.Features.Home.Views.Dialogs;

namespace TimeLogger.App.Features.Home.Views;

public partial class ToDoView : UserControl
{
    private readonly ToDoStore _store = new();
    public ToDoView() { InitializeComponent(); AttachedToVisualTree += (_, _) => Refresh(); }
    public async void Refresh()
    {
        try
        {
            var all = _store.LoadAll();
            ItemsList.ItemsSource = all.Where(x => !x.IsCompleted).ToList();
            ArchiveGroups.ItemsSource = all.Where(x => x.IsCompleted)
                .GroupBy(x => x.CompletedAt is { } completed ? (DateTime?)new DateTime(completed.ToLocalTime().Year, completed.ToLocalTime().Month, 1) : null)
                .OrderByDescending(x => x.Key ?? DateTime.MinValue)
                .Select(x => new ArchiveMonth { MonthLabel = x.Key?.ToString("MMMM yyyy") ?? "Completion date unknown", Items = x.OrderByDescending(y => y.CompletedAt).ToList() }).ToList();
        }
        catch (Exception ex) { await Error("Could not load to-dos", ex.Message); }
    }
    private async void OnAdd(object? sender, RoutedEventArgs e)
    {
        try { _store.Add(NewTitle.Text ?? "", NewDue.SelectedDate, NewNotes.Text ?? ""); NewTitle.Text = ""; NewNotes.Text = ""; NewDue.SelectedDate = null; Refresh(); }
        catch (Exception ex) { await Error("Could not add to-do", ex.Message); }
    }
    private void OnSelected(object? sender, SelectionChangedEventArgs e)
    {
        var item = ItemsList.SelectedItem as ToDoItem;
        EditTitle.IsEnabled = EditDue.IsEnabled = EditNotes.IsEnabled = SaveButton.IsEnabled = CompleteButton.IsEnabled = item is not null;
        EditTitle.Text = item?.Title ?? "";
        EditNotes.Text = item?.Notes ?? "";
        EditDue.SelectedDate = item?.DueDate;
        CompleteButton.Content = "Complete";
    }
    private async void OnSave(object? sender, RoutedEventArgs e)
    {
        if (ItemsList.SelectedItem is not ToDoItem old) return;
        try { var updated = Copy(old, EditTitle.Text ?? "", EditDue.SelectedDate, old.IsCompleted); updated.Notes = EditNotes.Text ?? ""; _store.Update(updated); Refresh(); }
        catch (Exception ex) { await Error("Could not save to-do", ex.Message); }
    }
    private async void OnComplete(object? sender, RoutedEventArgs e)
    {
        if (ItemsList.SelectedItem is not ToDoItem old) return;
        try { var completed = Copy(old, EditTitle.Text ?? "", EditDue.SelectedDate, true); completed.Notes = EditNotes.Text ?? ""; completed.CompletedAt = DateTimeOffset.Now; _store.Update(completed); Refresh(); }
        catch (Exception ex) { await Error("Could not update to-do", ex.Message); }
    }
    private async void OnRestore(object? sender, RoutedEventArgs e)
    {
        if (sender is not Button { DataContext: ToDoItem item }) return;
        try { var restored = Copy(item, item.Title, item.DueDate, false); restored.CompletedAt = null; _store.Update(restored); Refresh(); }
        catch (Exception ex) { await Error("Could not restore to-do", ex.Message); }
    }
    private void OnListTabChanged(object? sender, SelectionChangedEventArgs e)
    {
        if (ReferenceEquals(e.Source, ToDoTabs) && EditPanel is not null) EditPanel.IsVisible = ToDoTabs.SelectedIndex == 0;
    }
    private static ToDoItem Copy(ToDoItem item, string title, DateTime? due, bool completed) => new() { Id = item.Id, Title = title.Trim(), Notes = item.Notes, DueDate = due?.Date, IsCompleted = completed, CompletedAt = item.CompletedAt, SourceKey = item.SourceKey };
    private async System.Threading.Tasks.Task Error(string title, string message)
    {
        if (TopLevel.GetTopLevel(this) is Window owner) await new MessageDialogWindow(title, message, "OK", null).ShowDialog<bool>(owner);
    }
}

public sealed class ArchiveMonth
{
    public string MonthLabel { get; init; } = "";
    public List<ToDoItem> Items { get; init; } = [];
}
