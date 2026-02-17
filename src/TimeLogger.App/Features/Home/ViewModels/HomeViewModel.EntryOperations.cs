using System;
using System.Linq;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.ViewModels;

public sealed partial class HomeViewModel
{
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
            Notes = Notes.Trim()
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
        return SelectedEntry is not null;
    }
}
