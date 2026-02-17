using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.Input;
using TimeLogger.App.Features.Home.Models;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.ViewModels;

namespace TimeLogger.App.Features.Home.ViewModels;

public sealed partial class HomeViewModel : ViewModelBase
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
}
