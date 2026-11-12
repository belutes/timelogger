using System;
using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using TimeLogger.App.Features.Home.Models;
using TimeLogger.App.Features.Home.Services;

namespace TimeLogger.App.Features.Home.Views.Dialogs;

public partial class MeetingNotesWindow : Window
{
    private readonly MeetingNotesStore _store = new();
    private string _meetingId = "";
    private string? _loadError;
    private MeetingNotes _notes = new();
    private string _meetingTitle = "";

    public MeetingNotesWindow() => InitializeComponent();

    public MeetingNotesWindow(CalEvent meeting) : this()
    {
        _meetingId = meeting.Id ?? "";
        _meetingTitle = meeting.Subject;
        SubjectText.Text = meeting.Subject;
        ScheduleText.Text = $"{meeting.Start:dddd, MMMM d, yyyy h:mm tt} – {meeting.End:dddd, MMMM d, yyyy h:mm tt}" + (meeting.IsAllDay ? " (all day)" : "");
        AttendeesText.Text = meeting.Attendees;
        try
        {
            _notes = _store.Load(_meetingId);
            NotesBox.Text = _notes.Notes;
            ExistingToDos.Text = _notes.Items.Count == 0 ? "No follow-ups yet." : string.Join(Environment.NewLine, _notes.Items.Select(x => $"{(x.IsCompleted ? "✓" : "•")} {x.Title}"));
        }
        catch (Exception ex)
        {
            _loadError = ex.Message;
            SaveButton.IsEnabled = false;
            NotesBox.IsReadOnly = true;
            ToDoBox.IsReadOnly = true;
        }
        Opened += async (_, _) =>
        {
            if (_loadError is not null)
                await new MessageDialogWindow("Meeting notes unavailable", $"Notes could not be loaded and will not be overwritten. {_loadError}", "OK", null).ShowDialog<bool>(this);
        };
    }

    public MeetingNotesWindow(MeetingReference meeting) : this(new CalEvent
    {
        Id = meeting.Id, Subject = meeting.Subject, Location = "", Start = meeting.Start,
        End = meeting.End, Attendees = meeting.Attendees, IsAllDay = meeting.IsAllDay
    }) { }

    private async void OnSaveClicked(object? sender, RoutedEventArgs e)
    {
        try
        {
            var current = _store.Load(_meetingId);
            current.Notes = NotesBox.Text ?? "";
            current.MeetingTitle = _meetingTitle;
            foreach (var line in (ToDoBox.Text ?? "").Replace("\r\n", "\n").Replace('\r', '\n').Split('\n'))
                if (!string.IsNullOrWhiteSpace(line)) current.Items.Add(new ToDoItem { Title = line.Trim() });
            _store.Save(_meetingId, current);
            Close();
        }
        catch (Exception ex)
        {
            await new MessageDialogWindow("Could not save meeting notes", ex.Message, "OK", null).ShowDialog<bool>(this);
        }
    }

    private void OnCancelClicked(object? sender, RoutedEventArgs e) => Close();
}
