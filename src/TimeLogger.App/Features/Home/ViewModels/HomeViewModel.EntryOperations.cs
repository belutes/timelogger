using System;
using System.Linq;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;
using TimeLogger.App.Features.Home.Services;

namespace TimeLogger.App.Features.Home.ViewModels;

public sealed partial class HomeViewModel
{
    public async Task FillFromMeetingAsync(CalEvent meeting)
    {
        if (_dialogs is null) return;
        if (meeting.IsAllDay || meeting.Start.Date != meeting.End.Date || meeting.End <= meeting.Start ||
            meeting.Start.Second != 0 || meeting.End.Second != 0)
        {
            await _dialogs.ShowAlertAsync("Meeting time cannot be filled", "This time log supports meetings within one day, with times accurate to the minute. Please enter this meeting manually.");
            return;
        }

        if (!string.IsNullOrWhiteSpace(Notes) && !await _dialogs.ShowConfirmationAsync(
                "Replace current entry?", "The current entry notes will be replaced with this meeting's details.", "Replace", "Cancel")) return;

        MeetingNotes savedNotes;
        try { savedNotes = new MeetingNotesStore().Load(meeting.Id ?? ""); }
        catch (Exception ex)
        {
            await _dialogs.ShowAlertAsync("Meeting notes unavailable", $"The meeting could not be copied because its saved notes could not be read. {ex.Message}");
            return;
        }

        var details = new StringBuilder();
        details.AppendLine(meeting.Subject);
        details.AppendLine($"Location: {meeting.Location}");
        details.AppendLine($"Organizer: {meeting.Organizer}");
        details.AppendLine($"Invitees: {meeting.Attendees}");
        details.AppendLine($"Availability: {meeting.Status}; Response: {meeting.Response}; Importance: {meeting.Importance}");
        details.AppendLine($"Categories: {meeting.Categories}");
        if (meeting.IsCancelled) details.AppendLine("Cancelled");
        var agenda = MeetingAgenda(meeting.Description);
        if (!string.IsNullOrWhiteSpace(agenda)) details.AppendLine($"Description: {agenda}");
        if (!string.IsNullOrWhiteSpace(savedNotes.Notes)) details.AppendLine($"Notes: {savedNotes.Notes}");
        if (savedNotes.Items.Count > 0) details.AppendLine($"To do: {string.Join("; ", savedNotes.Items.Select(x => x.Title))}");

        SelectedDate = meeting.Start.Date;
        var start = meeting.Start.ToString("hh:mm tt", CultureInfo.InvariantCulture);
        var end = meeting.End.ToString("hh:mm tt", CultureInfo.InvariantCulture);
        AddTimeOption(start);
        AddTimeOption(end);
        _isUpdatingTimes = true;
        SelectedStartTime = start;
        SelectedEndTime = end;
        _isUpdatingTimes = false;
        var task = TaskOptions.FirstOrDefault(x => string.Equals(x, "Meeting", StringComparison.OrdinalIgnoreCase));
        if (task is null)
        {
            task = "Meeting";
            TaskOptions.Insert(Math.Max(0, TaskOptions.IndexOf("Other")), task);
        }
        SelectedTask = task;
        Notes = details.ToString().TrimEnd();
        _pendingMeeting = new MeetingReference { Id = meeting.Id!, Subject = meeting.Subject, Start = meeting.Start, End = meeting.End, Attendees = meeting.Attendees, IsAllDay = meeting.IsAllDay };
    }

    private static string MeetingAgenda(string? description)
    {
        if (string.IsNullOrWhiteSpace(description) || description == "Not provided") return "";
        var lines = description.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var end = lines.Length;
        for (var i = 0; i < lines.Length; i++)
        {
            var line = lines[i].Trim();
            if (Regex.IsMatch(line, @"^(?:Microsoft Teams(?: meeting| Need help\?)?|Join (?:the meeting now|Microsoft Teams Meeting|on your computer|on the web)|Click here to join the meeting|Meeting ID:|Dial in by phone|Join Zoom Meeting)\b", RegexOptions.IgnoreCase))
            {
                end = i;
                while (end > 0 && (string.IsNullOrWhiteSpace(lines[end - 1]) || Regex.IsMatch(lines[end - 1].Trim(), @"^[_\-─━]{3,}$"))) end--;
                break;
            }
        }
        return string.Join(Environment.NewLine, lines.Take(end)).Trim();
    }

    private void AddTimeOption(string value)
    {
        if (TimeOptions.Contains(value)) return;
        if (!TryParseTime(value, out var time)) return;
        var index = 0;
        while (index < TimeOptions.Count && TryParseTime(TimeOptions[index], out var existing) && existing < time) index++;
        TimeOptions.Insert(index, value);
    }

    private async Task HandleTaskSelectionChangedAsync(string? selectedTask)
    {
        if (_suppressTaskSelectionPrompt)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedTask))
        {
            return;
        }

        if (!string.Equals(selectedTask, "Other", StringComparison.Ordinal))
        {
            _lastNonOtherTask = selectedTask;
            return;
        }

        if (_dialogs is null)
        {
            return;
        }

        var customTaskName = await _dialogs.ShowCustomTaskPromptAsync();
        if (string.IsNullOrWhiteSpace(customTaskName))
        {
            _suppressTaskSelectionPrompt = true;
            SelectedTask = _lastNonOtherTask;
            _suppressTaskSelectionPrompt = false;
            return;
        }

        var customOption = $"Other - {customTaskName.Trim()}";
        if (!TaskOptions.Contains(customOption))
        {
            var otherIndex = TaskOptions.IndexOf("Other");
            var insertIndex = otherIndex >= 0 ? otherIndex : TaskOptions.Count;
            TaskOptions.Insert(insertIndex, customOption);
        }

        _suppressTaskSelectionPrompt = true;
        SelectedTask = customOption;
        _suppressTaskSelectionPrompt = false;
        _lastNonOtherTask = customOption;
    }

    private async Task AddEntryAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        if (!await TryBuildEntryFromFormAsync())
        {
            return;
        }

        var entry = BuildEntryFromForm();
        if (entry is null)
        {
            return;
        }

        if (HasOverlap(entry, null))
        {
            await _dialogs.ShowAlertAsync("Overlapping Time", "This time range overlaps an existing task entry.");
            return;
        }

        WorkEntries.Add(entry);
        SortEntries();
        RecalculateTotalDuration();
        OnPropertyChanged(nameof(EntryCountLabel));

        if (!await SaveCurrentDateEntriesAsync())
        {
            WorkEntries.Remove(entry);
            RecalculateTotalDuration();
            OnPropertyChanged(nameof(EntryCountLabel));
            return;
        }

        Notes = string.Empty;
        _pendingMeeting = null;
        AdvanceTimeAfterAdd(entry.End);
    }

    private async Task DeleteSelectedAsync()
    {
        if (SelectedEntry is null || _dialogs is null)
        {
            return;
        }

        var shouldDelete = await _dialogs.ShowConfirmationAsync(
            "Delete Entry",
            "Delete the selected task entry?",
            "Delete",
            "Cancel");

        if (!shouldDelete)
        {
            return;
        }

        var entryToDelete = SelectedEntry;
        WorkEntries.Remove(entryToDelete);
        SelectedEntry = null;
        RecalculateTotalDuration();
        OnPropertyChanged(nameof(EntryCountLabel));

        if (!await SaveCurrentDateEntriesAsync())
        {
            WorkEntries.Add(entryToDelete);
            SortEntries();
            RecalculateTotalDuration();
            OnPropertyChanged(nameof(EntryCountLabel));
        }
    }

    private async Task EditSelectedEntryAsync()
    {
        if (SelectedEntry is null || _dialogs is null)
        {
            return;
        }

        var edited = await _dialogs.ShowEditEntryAsync(SelectedEntry, TimeOptions);
        if (edited is null)
        {
            return;
        }

        if (!TryParseTime(edited.StartTime, out var start) ||
            !TryParseTime(edited.EndTime, out var end))
        {
            await _dialogs.ShowAlertAsync("Invalid Time", "Start and End times must be valid.");
            return;
        }

        if (end <= start)
        {
            await _dialogs.ShowAlertAsync("Invalid Time Range", "End time must be later than start time.");
            return;
        }

        if (string.IsNullOrWhiteSpace(edited.TaskName) || string.IsNullOrWhiteSpace(edited.Notes))
        {
            await _dialogs.ShowAlertAsync("Missing Fields", "Task and Notes are required.");
            return;
        }

        var candidate = new WorkEntry
        {
            Id = SelectedEntry.Id,
            Date = SelectedEntry.Date,
            Start = start,
            End = end,
            Task = edited.TaskName.Trim(),
            Notes = edited.Notes.Trim()
        };

        if (HasOverlap(candidate, SelectedEntry.Id))
        {
            await _dialogs.ShowAlertAsync("Overlapping Time", "This time range overlaps an existing task entry.");
            return;
        }

        SelectedEntry.Start = candidate.Start;
        SelectedEntry.End = candidate.End;
        SelectedEntry.Task = candidate.Task;
        SelectedEntry.Notes = candidate.Notes;

        SortEntries();
        RecalculateTotalDuration();

        if (!await SaveCurrentDateEntriesAsync())
        {
            await LoadEntriesForSelectedDateAsync();
        }
    }

    private async Task<bool> TryBuildEntryFromFormAsync()
    {
        if (_dialogs is null)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(SelectedTask) || string.Equals(SelectedTask, "Other", StringComparison.Ordinal))
        {
            await _dialogs.ShowAlertAsync("Missing Task", "Select a task before adding the entry.");
            return false;
        }

        if (string.IsNullOrWhiteSpace(Notes))
        {
            await _dialogs.ShowAlertAsync("Missing Notes", "Notes are required for each entry.");
            return false;
        }

        if (!TryParseTime(SelectedStartTime, out var start) || !TryParseTime(SelectedEndTime, out var end))
        {
            await _dialogs.ShowAlertAsync("Invalid Time", "Start and End times must be valid.");
            return false;
        }

        if (end <= start)
        {
            await _dialogs.ShowAlertAsync("Invalid Time Range", "End time must be later than start time.");
            return false;
        }

        return true;
    }

    private WorkEntry? BuildEntryFromForm()
    {
        if (string.IsNullOrWhiteSpace(SelectedTask) ||
            string.IsNullOrWhiteSpace(Notes) ||
            !TryParseTime(SelectedStartTime, out var start) ||
            !TryParseTime(SelectedEndTime, out var end) ||
            end <= start)
        {
            return null;
        }

        return new WorkEntry
        {
            Id = Guid.NewGuid(),
            Date = (SelectedDate ?? DateTime.Today).Date,
            Start = start,
            End = end,
            Task = SelectedTask!,
            Notes = Notes.Trim(),
            Meeting = _pendingMeeting
        };
    }

    private bool HasOverlap(WorkEntry candidate, Guid? ignoreEntryId)
    {
        foreach (var existing in WorkEntries)
        {
            if (ignoreEntryId.HasValue && existing.Id == ignoreEntryId.Value)
            {
                continue;
            }

            var overlaps = candidate.Start < existing.End && existing.Start < candidate.End;
            if (overlaps)
            {
                return true;
            }
        }

        return false;
    }

    private void SortEntries()
    {
        var sorted = WorkEntries.OrderBy(entry => entry.Start).ToList();
        WorkEntries.Clear();
        foreach (var entry in sorted)
        {
            WorkEntries.Add(entry);
        }
    }

    private void RecalculateTotalDuration()
    {
        var totalMinutes = WorkEntries.Sum(entry => (entry.End - entry.Start).TotalMinutes);
        var total = TimeSpan.FromMinutes(totalMinutes);
        TotalDuration = $"{(int)total.TotalHours}h {total.Minutes}m";
    }

    private bool CanDeleteSelected()
    {
        return _entriesReady && SelectedEntry is not null;
    }
}
