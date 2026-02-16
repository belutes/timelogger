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
    private readonly ICalendarService _calendarService;
    private readonly ITimeLogExportService _exportService;
    private IHomeDialogService? _dialogs;

    private DateTime? _selectedDate = DateTime.Today;
    private string? _selectedTask;
    private string _notes = string.Empty;
    private string _selectedStartTime = string.Empty;
    private string _selectedEndTime = string.Empty;
    private string _totalDuration = "0h 0m";
    private string _selectedCalendarRange = "Today";
    private WorkEntry? _selectedEntry;
    private string? _lastNonOtherTask;
    private bool _suppressTaskSelectionPrompt;
    private bool _isUpdatingTimes;
    private string? _lastCalendarError;

    public HomeViewModel(
        ITimeLogStorageService storage,
        ICalendarService calendarService,
        ITimeLogExportService exportService)
    {
        _storage = storage;
        _calendarService = calendarService;
        _exportService = exportService;

        TimeOptions = new ObservableCollection<string>(BuildTimeOptions());
        SetDefaultWorkdayTimes();

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
        CalendarEvents = new ObservableCollection<CalEvent>();

        QuickRanges = new ObservableCollection<string>
        {
            "Today",
            "This Week",
            "This Month"
        };
        SelectedCalendarRange = QuickRanges[0];

        AddEntryCommand = new AsyncRelayCommand(AddEntryAsync);
        DeleteSelectedCommand = new AsyncRelayCommand(DeleteSelectedAsync, CanDeleteSelected);
        EditSelectedEntryCommand = new AsyncRelayCommand(EditSelectedEntryAsync, CanDeleteSelected);
        RefreshCalendarCommand = new AsyncRelayCommand(LoadCalendarEventsAsync);
        ExportDayCsvCommand = new AsyncRelayCommand(ExportDayCsvAsync);
        ExportRangeCsvCommand = new AsyncRelayCommand(ExportRangeCsvAsync);
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
                SetDefaultWorkdayTimes();
                _ = LoadEntriesForSelectedDateAsync();
                _ = LoadCalendarEventsAsync();
            }
        }
    }

    public string SelectedDateDisplay => (SelectedDate ?? DateTime.Today).ToString("MM/dd/yy");
    public string CalendarGroupLabel => $"Today - {(SelectedDate ?? DateTime.Today):ddd, MMM d, yyyy}";

    public ObservableCollection<string> TimeOptions { get; }

    public string SelectedStartTime
    {
        get => _selectedStartTime;
        set
        {
            if (SetProperty(ref _selectedStartTime, value))
            {
                if (_isUpdatingTimes)
                {
                    return;
                }

                if (TryGetNextTimeOption(value, out var nextTime))
                {
                    _isUpdatingTimes = true;
                    SelectedEndTime = nextTime;
                    _isUpdatingTimes = false;
                }
            }
        }
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

    public ObservableCollection<CalEvent> CalendarEvents { get; }
    public ObservableCollection<string> QuickRanges { get; }
    public string SelectedCalendarRange
    {
        get => _selectedCalendarRange;
        set
        {
            if (SetProperty(ref _selectedCalendarRange, value))
            {
                _ = LoadCalendarEventsAsync();
            }
        }
    }

    public IAsyncRelayCommand AddEntryCommand { get; }
    public IAsyncRelayCommand DeleteSelectedCommand { get; }
    public IAsyncRelayCommand EditSelectedEntryCommand { get; }
    public IAsyncRelayCommand RefreshCalendarCommand { get; }
    public IAsyncRelayCommand ExportDayCsvCommand { get; }
    public IAsyncRelayCommand ExportRangeCsvCommand { get; }

    public void AttachDialogService(IHomeDialogService dialogs)
    {
        _dialogs = dialogs;
        _ = LoadEntriesForSelectedDateAsync();
        _ = LoadCalendarEventsAsync();
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

    private async Task LoadCalendarEventsAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        try
        {
            var (start, endExclusive) = ResolveCalendarRange();
            var events = await _calendarService.GetEventsAsync(start, endExclusive);

            CalendarEvents.Clear();
            foreach (var calendarEvent in events)
            {
                CalendarEvents.Add(calendarEvent);
            }

            _lastCalendarError = null;
            OnPropertyChanged(nameof(EventCountLabel));
        }
        catch (Exception ex)
        {
            var message = $"Could not load Outlook events.\n\n{ex.Message}";
            if (string.Equals(message, _lastCalendarError, StringComparison.Ordinal))
            {
                return;
            }

            _lastCalendarError = message;
            await _dialogs.ShowAlertAsync("Outlook Calendar", message);
        }
    }

    private async Task ExportDayCsvAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        var selectedDate = (SelectedDate ?? DateTime.Today).Date;
        if (!await EnsureRecordsDirectoryAsync(
                $"Create '{_storage.RecordsDirectoryPath}' to store export files?",
                "Export Cancelled",
                "CSV export was cancelled because the records folder was not created."))
        {
            return;
        }

        string? calendarFetchError = null;
        IReadOnlyList<CalEvent> dayCalendarEvents;
        try
        {
            var start = new DateTimeOffset(selectedDate);
            var endExclusive = start.AddDays(1);
            dayCalendarEvents = await _calendarService.GetEventsAsync(start, endExclusive);
        }
        catch (Exception ex)
        {
            calendarFetchError = ex.Message;
            dayCalendarEvents = CalendarEvents
                .Where(item => item.Start.Date == selectedDate)
                .OrderBy(item => item.Start)
                .ToList();
        }

        var csvPath = await _exportService.ExportDayCsvAsync(
            _storage.RecordsDirectoryPath,
            selectedDate,
            WorkEntries.ToList(),
            dayCalendarEvents,
            TotalDuration,
            calendarFetchError);

        var message = $"Exported day CSV for {selectedDate:yyyy-MM-dd}.\n\n{csvPath}";
        if (!string.IsNullOrWhiteSpace(calendarFetchError))
        {
            message += "\n\nCalendar events were exported from the currently loaded list because live calendar fetch failed.";
        }

        await _dialogs.ShowAlertAsync("Export Complete", message);
    }

    private async Task ExportRangeCsvAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        var defaultDate = (SelectedDate ?? DateTime.Today).Date;
        var rangeInput = await _dialogs.ShowDateRangePromptAsync(defaultDate, defaultDate);
        if (rangeInput is null)
        {
            return;
        }

        var startDate = rangeInput.StartDate.Date;
        var endDate = rangeInput.EndDate.Date;
        if (endDate < startDate)
        {
            await _dialogs.ShowAlertAsync("Invalid Range", "End date must be the same or later than start date.");
            return;
        }

        if (!await EnsureRecordsDirectoryAsync(
                $"Create '{_storage.RecordsDirectoryPath}' to store export files?",
                "Export Cancelled",
                "CSV export was cancelled because the records folder was not created."))
        {
            return;
        }

        var rangeEntries = await _storage.LoadEntriesInRangeAsync(startDate, endDate);

        string? calendarFetchError = null;
        IReadOnlyList<CalEvent> rangeCalendarEvents;
        try
        {
            var start = new DateTimeOffset(startDate);
            var endExclusive = new DateTimeOffset(endDate.AddDays(1));
            rangeCalendarEvents = await _calendarService.GetEventsAsync(start, endExclusive);
        }
        catch (Exception ex)
        {
            calendarFetchError = ex.Message;
            rangeCalendarEvents = CalendarEvents
                .Where(item => item.Start.Date >= startDate && item.Start.Date <= endDate)
                .OrderBy(item => item.Start)
                .ToList();
        }

        var csvPath = await _exportService.ExportRangeCsvAsync(
            _storage.RecordsDirectoryPath,
            startDate,
            endDate,
            rangeEntries,
            rangeCalendarEvents,
            calendarFetchError);

        var message = $"Exported range CSV for {startDate:yyyy-MM-dd} to {endDate:yyyy-MM-dd}.\n\n{csvPath}";
        if (!string.IsNullOrWhiteSpace(calendarFetchError))
        {
            message += "\n\nCalendar events were exported from the currently loaded list because live calendar fetch failed.";
        }

        await _dialogs.ShowAlertAsync("Export Complete", message);
    }

    private (DateTimeOffset start, DateTimeOffset endExclusive) ResolveCalendarRange()
    {
        var day = (SelectedDate ?? DateTime.Today).Date;
        return SelectedCalendarRange switch
        {
            "This Week" => (StartOfWeek(day), StartOfWeek(day).AddDays(7)),
            "This Month" =>
                (new DateTimeOffset(new DateTime(day.Year, day.Month, 1)), new DateTimeOffset(new DateTime(day.Year, day.Month, 1).AddMonths(1))),
            _ => (new DateTimeOffset(day), new DateTimeOffset(day.AddDays(1)))
        };
    }

    private static DateTimeOffset StartOfWeek(DateTime day)
    {
        var delta = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return new DateTimeOffset(day.AddDays(-delta));
    }

    private async Task<bool> SaveCurrentDateEntriesAsync()
    {
        if (_dialogs is null)
        {
            return false;
        }

        if (!await EnsureRecordsDirectoryAsync(
                $"Create '{_storage.RecordsDirectoryPath}' to store time log records?",
                "Save Cancelled",
                "Entry was not saved because the records folder was not created."))
        {
            return false;
        }

        var date = (SelectedDate ?? DateTime.Today).Date;
        await _storage.SaveEntriesForDateAsync(date, WorkEntries.ToList());
        return true;
    }

    private async Task<bool> EnsureRecordsDirectoryAsync(string confirmationMessage, string cancelTitle, string cancelMessage)
    {
        if (_dialogs is null)
        {
            return false;
        }

        if (_storage.RecordsDirectoryExists())
        {
            return true;
        }

        var shouldCreate = await _dialogs.ShowConfirmationAsync(
            "Create Records Folder",
            confirmationMessage,
            "Create",
            "Cancel");

        if (!shouldCreate)
        {
            await _dialogs.ShowAlertAsync(cancelTitle, cancelMessage);
            return false;
        }

        _storage.CreateRecordsDirectory();
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

    private void SetDefaultWorkdayTimes()
    {
        _isUpdatingTimes = true;
        SelectedStartTime = "08:00 AM";
        SelectedEndTime = "08:15 AM";
        _isUpdatingTimes = false;
    }

    private void AdvanceTimeAfterAdd(TimeSpan endTime)
    {
        var nextStart = DateTime.Today.Add(endTime).ToString("hh:mm tt");

        _isUpdatingTimes = true;
        SelectedStartTime = nextStart;
        _isUpdatingTimes = false;

        if (TryGetNextTimeOption(nextStart, out var nextEnd))
        {
            SelectedEndTime = nextEnd;
            return;
        }

        SelectedEndTime = nextStart;
    }

    private bool TryGetNextTimeOption(string current, out string next)
    {
        next = string.Empty;
        var index = TimeOptions.IndexOf(current);
        if (index < 0)
        {
            return false;
        }

        var nextIndex = index + 1;
        if (nextIndex >= TimeOptions.Count)
        {
            return false;
        }

        next = TimeOptions[nextIndex];
        return true;
    }
}
