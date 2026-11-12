using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Collections.Generic;
using System.Linq;

namespace TimeLogger.App.Features.Home.Services;

public sealed class MeetingNotes
{
    public string Notes { get; set; } = "";
    public string ToDo { get; set; } = "";
    public string MeetingTitle { get; set; } = "";
    public List<ToDoItem> Items { get; set; } = [];
}

public sealed class ToDoItem
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "";
    public string Notes { get; set; } = "";
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    [System.Text.Json.Serialization.JsonIgnore] public string SourceKey { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string SourceLabel { get; set; } = "";
    [System.Text.Json.Serialization.JsonIgnore] public string DueLabel => DueDate is { } due ? $"Due {due:MMM d, yyyy}" : "No due date";
    [System.Text.Json.Serialization.JsonIgnore] public string StatusLabel => IsCompleted ? "Completed" : "Open";
    [System.Text.Json.Serialization.JsonIgnore] public string CompletedLabel => CompletedAt is { } completed ? $"Completed {completed.ToLocalTime():MMM d, yyyy}" : "Completion date unknown";
}

public sealed class MeetingNotesStore
{
    private readonly string _directory;

    public MeetingNotesStore(string? directory = null) => _directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "TimeLogger", "MeetingNotes");

    private static string KeyFor(string meetingId)
    {
        if (string.IsNullOrWhiteSpace(meetingId)) throw new InvalidOperationException("This meeting has no calendar ID, so notes cannot be saved safely.");
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(meetingId)));
        return hash;
    }

    private string PathForKey(string key)
    {
        if (key.Length != 64 || key.Any(c => !Uri.IsHexDigit(c))) throw new InvalidDataException("Invalid meeting notes reference.");
        return Path.Combine(_directory, key + ".json");
    }

    public MeetingNotes Load(string meetingId)
    {
        var path = PathForKey(KeyFor(meetingId));
        if (!File.Exists(path)) return new MeetingNotes();
        return Read(path);
    }

    private static MeetingNotes Read(string path)
    {
        var notes = JsonSerializer.Deserialize<MeetingNotes>(File.ReadAllText(path))
            ?? throw new InvalidDataException("The meeting notes file is empty.");
        notes.Items ??= [];
        if (!string.IsNullOrWhiteSpace(notes.ToDo))
        {
            var legacy = notes.ToDo.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (var i = 0; i < legacy.Length; i++)
                if (!string.IsNullOrWhiteSpace(legacy[i])) notes.Items.Add(new ToDoItem { Id = $"legacy-{i}", Title = legacy[i].Trim() });
            notes.ToDo = "";
        }
        return notes;
    }

    public IReadOnlyList<ToDoItem> GetMeetingItems()
    {
        var result = new List<ToDoItem>();
        if (!Directory.Exists(_directory)) return result;
        foreach (var path in Directory.GetFiles(_directory, "*.json"))
        {
            var key = Path.GetFileNameWithoutExtension(path);
            var notes = Read(path);
            foreach (var item in notes.Items)
            {
                item.SourceKey = key;
                item.SourceLabel = string.IsNullOrWhiteSpace(notes.MeetingTitle) ? "Meeting follow-up" : $"Meeting: {notes.MeetingTitle}";
                result.Add(item);
            }
        }
        return result;
    }

    public void UpdateMeetingItem(ToDoItem item)
    {
        var path = PathForKey(item.SourceKey);
        var notes = Read(path);
        var index = notes.Items.FindIndex(x => x.Id == item.Id);
        if (index < 0) throw new InvalidOperationException("This follow-up no longer exists. Refresh the list and try again.");
        notes.Items[index] = item;
        Write(path, notes);
    }

    public void Save(string meetingId, MeetingNotes notes)
    {
        var path = PathForKey(KeyFor(meetingId));
        Write(path, notes);
    }

    private void Write(string path, MeetingNotes notes)
    {
        Directory.CreateDirectory(_directory);
        var temp = Path.Combine(_directory, $".notes-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temp, JsonSerializer.Serialize(notes, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temp, path, true);
        }
        finally { if (File.Exists(temp)) File.Delete(temp); }
    }
}
