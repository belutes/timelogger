using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace TimeLogger.App.Features.Home.Services;

public sealed class ToDoStore
{
    private readonly string _path;
    private readonly MeetingNotesStore _meetings;
    public ToDoStore(string? directory = null)
    {
        directory ??= Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeLogger");
        _path = Path.Combine(directory, "todos.json");
        _meetings = new MeetingNotesStore(Path.Combine(directory, "MeetingNotes"));
    }
    private List<ToDoItem> Read() => !File.Exists(_path) ? [] :
        JsonSerializer.Deserialize<List<ToDoItem>>(File.ReadAllText(_path)) ?? throw new InvalidDataException("The to-do file is empty.");

    public List<ToDoItem> LoadAll()
    {
        var items = Read();
        foreach (var item in items) item.SourceLabel = "Personal to-do";
        items.AddRange(_meetings.GetMeetingItems());
        return items.OrderBy(x => x.IsCompleted).ThenBy(x => x.DueDate ?? DateTime.MaxValue).ThenBy(x => x.Title).ToList();
    }
    public void Add(string title, DateTime? due, string notes = "")
    {
        if (string.IsNullOrWhiteSpace(title)) throw new ArgumentException("Enter a to-do item.");
        var items = Read();
        items.Add(new ToDoItem { Title = title.Trim(), DueDate = due?.Date, Notes = notes });
        Write(items);
    }
    public void Update(ToDoItem item)
    {
        if (string.IsNullOrWhiteSpace(item.Title)) throw new ArgumentException("Enter a to-do item.");
        if (!string.IsNullOrEmpty(item.SourceKey)) { _meetings.UpdateMeetingItem(item); return; }
        var items = Read();
        var index = items.FindIndex(x => x.Id == item.Id);
        if (index < 0) throw new InvalidOperationException("This to-do no longer exists. Refresh and try again.");
        items[index] = item;
        Write(items);
    }
    private void Write(List<ToDoItem> items)
    {
        var dir = Path.GetDirectoryName(_path)!;
        Directory.CreateDirectory(dir);
        var temp = Path.Combine(dir, $".todos-{Guid.NewGuid():N}.tmp");
        try { File.WriteAllText(temp, JsonSerializer.Serialize(items, new JsonSerializerOptions { WriteIndented = true })); File.Move(temp, _path, true); }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
