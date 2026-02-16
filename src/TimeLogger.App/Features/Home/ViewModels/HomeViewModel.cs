using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.Input;
using TimeLogger.App.Features.Home.Models;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.ViewModels;

namespace TimeLogger.App.Features.Home.ViewModels;

public sealed class HomeViewModel : ViewModelBase
{
    private readonly ITimeLogStorageService _storage;
    private IHomeDialogService? _dialogs;

    private DateTime? _selectedDate = DateTime.Today;
    private string? _selectedTask;
    private string _notes = string.Empty;
    private string _selectedStartTime = string.Empty;
    private string _selectedEndTime = string.Empty;
    private string _totalDuration = "0h 0m";
    private WorkEntry? _selectedEntry;
    private string? _lastNonOtherTask;
    private bool _suppressTaskSelectionPrompt;

    public HomeViewModel(ITimeLogStorageService storage)
    {
        _storage = storage;

        TimeOptions = new ObservableCollection<string>(BuildTimeOptions());
        SelectedStartTime = "08:00 AM";
        SelectedEndTime = "08:15 AM";

        TaskOptions = new ObservableCollection<string>
        {
            "Tickets",
            "Meeting",
            "Power BI",
            "QA Testing",
            "Knowledge Base Development",
            "Other"
        };

        SelectedTask = TaskOptions[0];
        _lastNonOtherTask = SelectedTask;

        WorkEntries = new ObservableCollection<WorkEntry>();
        CalendarEvents = new ObservableCollection<CalendarEvent>
        {
            new CalendarEvent
            {
                Title = "Team Sync",
                DateLine = "Sunday, February 15, 2026",
                TimeRange = "09:00-09:30 AM",
                Location = "Online"
            },
            new CalendarEvent
            {
                Title = "Focus Block",
                DateLine = "Sunday, February 15, 2026",
                TimeRange = "09:45-11:15 AM",
                Location = "Desk"
            },
            new CalendarEvent
            {
                Title = "Team Sync",
                DateLine = "Sunday, February 15, 2026",
                TimeRange = "10:30-11:30 AM",
                Location = "Online"
            },
            new CalendarEvent
            {
                Title = "Focus Block",
                DateLine = "Sunday, February 15, 2026",
                TimeRange = "11:15-12:45 AM",
                Location = "Desk"
            }
        };

        QuickRanges = new ObservableCollection<string>
        {
            "Today",
            "This Week",
            "This Month"
        };

        AddEntryCommand = new AsyncRelayCommand(AddEntryAsync);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, CanDeleteSelected);
        EditSelectedEntryCommand = new AsyncRelayCommand(EditSelectedEntryAsync, CanDeleteSelected);
    }

    public string HeaderTitle { get; } = "Daily Time Log";

    public DateTime? SelectedDate
    {
        get => _selectedDate;
        set
        {
            if (SetProperty(ref _selectedDate, value))
            {
                OnPropertyChanged(nameof(SelectedDateDisplay));
                OnPropertyChanged(nameof(CalendarGroupLabel));
                _ = LoadEntriesForSelectedDateAsync();
            }
        }
    }

    public string SelectedDateDisplay => (SelectedDate ?? DateTime.Today).ToString("MM/dd/yy");
    public string CalendarGroupLabel => $"Today - {(SelectedDate ?? DateTime.Today):ddd, MMM d, yyyy}";

    public ObservableCollection<string> TimeOptions { get; }

    public string SelectedStartTime
    {
        get => _selectedStartTime;
        set => SetProperty(ref _selectedStartTime, value);
    }

    public string SelectedEndTime
    {
        get => _selectedEndTime;
        set => SetProperty(ref _selectedEndTime, value);
    }

    public ObservableCollection<string> TaskOptions { get; }

    public string? SelectedTask
    {
        get => _selectedTask;
        set
        {
            if (SetProperty(ref _selectedTask, value))
            {
                _ = HandleTaskSelectionChangedAsync(value);
            }
        }
    }

    public string Notes
    {
        get => _notes;
        set => SetProperty(ref _notes, value);
    }

    public string TotalDuration
    {
        get => _totalDuration;
        private set => SetProperty(ref _totalDuration, value);
    }

    public ObservableCollection<WorkEntry> WorkEntries { get; }

    public WorkEntry? SelectedEntry
    {
        get => _selectedEntry;
        set
        {
            if (SetProperty(ref _selectedEntry, value))
            {
                DeleteSelectedCommand.NotifyCanExecuteChanged();
                EditSelectedEntryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    public string EventCountLabel => $"{CalendarEvents.Count} event(s).";
    public string EntryCountLabel => $"{WorkEntries.Count} entries";

    public ObservableCollection<CalendarEvent> CalendarEvents { get; }
    public ObservableCollection<string> QuickRanges { get; }

    public IAsyncRelayCommand AddEntryCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand EditSelectedEntryCommand { get; }

    public void AttachDialogService(IHomeDialogService dialogs)
    {
        _dialogs = dialogs;
        _ = LoadEntriesForSelectedDateAsync();
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

    private async Task LoadEntriesForSelectedDateAsync()
    {
        var date = (SelectedDate ?? DateTime.Today).Date;
        var loadedEntries = await _storage.LoadEntriesForDateAsync(date);

        WorkEntries.Clear();
        foreach (var entry in loadedEntries.OrderBy(item => item.Start))
        {
            WorkEntries.Add(entry);
        }

        SelectedEntry = null;
        RecalculateTotalDuration();
        OnPropertyChanged(nameof(EntryCountLabel));
    }

    private async Task<bool> SaveCurrentDateEntriesAsync()
    {
        if (_dialogs is null)
        {
            return false;
        }

        if (!_storage.RecordsDirectoryExists())
        {
            var shouldCreate = await _dialogs.ShowConfirmationAsync(
                "Create Records Folder",
                $"Create '{_storage.RecordsDirectoryPath}' to store time log records?",
                "Create",
                "Cancel");

            if (!shouldCreate)
            {
                await _dialogs.ShowAlertAsync("Save Cancelled", "Entry was not saved because the records folder was not created.");
                return false;
            }

            _storage.CreateRecordsDirectory();
        }

        var date = (SelectedDate ?? DateTime.Today).Date;
        await _storage.SaveEntriesForDateAsync(date, WorkEntries.ToList());
        return true;
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

    private static bool TryParseTime(string? value, out TimeSpan time)
    {
        time = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var formats = new[] { "h:mm tt", "hh:mm tt" };
        if (!DateTime.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        time = parsed.TimeOfDay;
        return true;
    }

    private static IEnumerable<string> BuildTimeOptions()
    {
        var time = DateTime.Today;
        for (var i = 0; i < 96; i++)
        {
            yield return time.AddMinutes(i * 15).ToString("hh:mm tt");
        }
    }
}
