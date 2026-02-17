using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Globalization;
using System.Linq;
using System.Threading;
using TimeLogger.App.Features.DataAnalysis.Models;
using TimeLogger.App.Features.Home.Models;
using TimeLogger.App.Features.Home.Services;
using TimeLogger.App.ViewModels;

namespace TimeLogger.App.Features.DataAnalysis.ViewModels;

public sealed class DataAnalysisViewModel : ViewModelBase
{
    private const double PieRadius = 105d;
    private const double PieCenter = 105d;

    private static readonly string[] PieColors =
    [
        "#7398E6",
        "#5CB978",
        "#F59E0B",
        "#EF4444",
        "#A78BFA",
        "#14B8A6"
    ];

    private readonly ITimeLogStorageService _storage;
    private readonly SemaphoreSlim _refreshGate = new(1, 1);

    private DateTime _analysisDate = DateTime.Today;
    private string _selectedDetailRange = "Day";
    private string _selectedBarMode = "Weekly";

    public DataAnalysisViewModel(ITimeLogStorageService storage)
    {
        _storage = storage;

        DetailRangeOptions = new ObservableCollection<string>
        {
            "Day",
            "Week",
            "Month"
        };

        DayBreakdown = new ObservableCollection<ActivityBreakdownItem>();
        PieSlices = new ObservableCollection<PieSliceItem>();
        DetailItems = new ObservableCollection<ActivityBreakdownItem>();
        BarItems = new ObservableCollection<ActivityBreakdownItem>();

        SelectWeeklyCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectWeekly);
        SelectMonthlyCommand = new CommunityToolkit.Mvvm.Input.RelayCommand(SelectMonthly);

        _ = InitializeAsync();
    }

    public ObservableCollection<string> DetailRangeOptions { get; }
    public ObservableCollection<ActivityBreakdownItem> DayBreakdown { get; }
    public ObservableCollection<PieSliceItem> PieSlices { get; }
    public ObservableCollection<ActivityBreakdownItem> DetailItems { get; }
    public ObservableCollection<ActivityBreakdownItem> BarItems { get; }

    public string SelectedDetailRange
    {
        get => _selectedDetailRange;
        set
        {
            if (SetProperty(ref _selectedDetailRange, value))
            {
                _ = RefreshDetailItemsAsync();
            }
        }
    }

    public string SelectedBarMode
    {
        get => _selectedBarMode;
        private set
        {
            if (SetProperty(ref _selectedBarMode, value))
            {
                _ = RefreshBarItemsAsync();
                OnPropertyChanged(nameof(IsWeeklySelected));
                OnPropertyChanged(nameof(IsMonthlySelected));
            }
        }
    }

    public string DayTitle => _analysisDate.ToString("dddd, MMM d, yyyy");
    public string DetailHeading
    {
        get
        {
            var day = _analysisDate.Date;
            return SelectedDetailRange switch
            {
                "Week" => $"Week - {StartOfWeek(day):MMM d}-{StartOfWeek(day).AddDays(6):MMM d, yyyy}",
                "Month" =>
                    $"Month - {new DateTime(day.Year, day.Month, 1):MMM d}-{new DateTime(day.Year, day.Month, DateTime.DaysInMonth(day.Year, day.Month)):MMM d, yyyy}",
                _ => $"Day - {day:ddd, MMM d, yyyy}"
            };
        }
    }

    public string BarHeading
    {
        get
        {
            var monthStart = new DateTime(_analysisDate.Year, _analysisDate.Month, 1);
            var monthEnd = new DateTime(_analysisDate.Year, _analysisDate.Month, DateTime.DaysInMonth(_analysisDate.Year, _analysisDate.Month));
            return SelectedBarMode == "Weekly"
                ? $"Weekly - {StartOfWeek(_analysisDate):MMM d}-{StartOfWeek(_analysisDate).AddDays(6):MMM d, yyyy}"
                : $"Monthly - {monthStart:MMM d}-{monthEnd:MMM d, yyyy}";
        }
    }

    public string TotalDurationText
    {
        get
        {
            var minutes = DetailItems.Sum(item => item.Minutes);
            return $"{minutes / 60}h {minutes % 60:00}m";
        }
    }

    public bool IsWeeklySelected => SelectedBarMode == "Weekly";
    public bool IsMonthlySelected => SelectedBarMode == "Monthly";

    public CommunityToolkit.Mvvm.Input.IRelayCommand SelectWeeklyCommand { get; }
    public CommunityToolkit.Mvvm.Input.IRelayCommand SelectMonthlyCommand { get; }

    private async System.Threading.Tasks.Task InitializeAsync()
    {
        await RefreshAsync();
    }

    public async System.Threading.Tasks.Task RefreshAsync()
    {
        await _refreshGate.WaitAsync();
        try
        {
            _analysisDate = await ResolveLatestAnalysisDateAsync(_analysisDate.Date);
            OnPropertyChanged(nameof(DayTitle));
            OnPropertyChanged(nameof(DetailHeading));
            OnPropertyChanged(nameof(BarHeading));

            await RefreshDayBreakdownAsync();
            await RefreshDetailItemsAsync();
            await RefreshBarItemsAsync();
        }
        catch
        {
            DayBreakdown.Clear();
            PieSlices.Clear();
            DetailItems.Clear();
            BarItems.Clear();
            OnPropertyChanged(nameof(TotalDurationText));
        }
        finally
        {
            _refreshGate.Release();
        }
    }

    private void SelectWeekly()
    {
        SelectedBarMode = "Weekly";
    }

    private void SelectMonthly()
    {
        SelectedBarMode = "Monthly";
    }

    private async System.Threading.Tasks.Task RefreshDayBreakdownAsync()
    {
        try
        {
            var entries = await _storage.LoadEntriesForDateAsync(_analysisDate.Date);
            var dayItems = BuildItemsFromEntries(entries, PieColors);
            SetCollection(DayBreakdown, dayItems);
            SetCollection(PieSlices, BuildPieSlices(dayItems));
        }
        catch
        {
            DayBreakdown.Clear();
            PieSlices.Clear();
        }
    }

    private async System.Threading.Tasks.Task RefreshDetailItemsAsync()
    {
        try
        {
            var (start, end) = ResolveDetailRange(_analysisDate.Date, SelectedDetailRange);
            var entries = await _storage.LoadEntriesInRangeAsync(start, end);
            SetCollection(DetailItems, BuildItemsFromEntries(entries, PieColors));
            OnPropertyChanged(nameof(DetailHeading));
            OnPropertyChanged(nameof(TotalDurationText));
        }
        catch
        {
            DetailItems.Clear();
            OnPropertyChanged(nameof(TotalDurationText));
        }
    }

    private async System.Threading.Tasks.Task RefreshBarItemsAsync()
    {
        try
        {
            var (start, end) = ResolveBarRange(_analysisDate.Date, SelectedBarMode);
            var entries = await _storage.LoadEntriesInRangeAsync(start, end);
            var barData = BuildItemsFromEntries(entries, ["#3B82F6"]);
            SetCollection(BarItems, barData);
            OnPropertyChanged(nameof(BarHeading));
        }
        catch
        {
            BarItems.Clear();
        }
    }

    private async System.Threading.Tasks.Task<DateTime> ResolveLatestAnalysisDateAsync(DateTime fallbackDate)
    {
        if (!_storage.RecordsDirectoryExists())
        {
            return fallbackDate.Date;
        }

        var now = DateTime.Today;
        var allRecentEntries = await _storage.LoadEntriesInRangeAsync(now.AddYears(-2), now);
        var latestDate = allRecentEntries
            .OrderByDescending(item => item.Date)
            .Select(item => item.Date.Date)
            .FirstOrDefault();

        return latestDate == default ? fallbackDate.Date : latestDate;
    }

    private static (DateTime start, DateTime endInclusive) ResolveDetailRange(DateTime anchorDate, string range)
    {
        return range switch
        {
            "Week" => (StartOfWeek(anchorDate), StartOfWeek(anchorDate).AddDays(6)),
            "Month" =>
                (new DateTime(anchorDate.Year, anchorDate.Month, 1), new DateTime(anchorDate.Year, anchorDate.Month, DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month))),
            _ => (anchorDate.Date, anchorDate.Date)
        };
    }

    private static (DateTime start, DateTime endInclusive) ResolveBarRange(DateTime anchorDate, string mode)
    {
        return mode == "Monthly"
            ? (new DateTime(anchorDate.Year, anchorDate.Month, 1), new DateTime(anchorDate.Year, anchorDate.Month, DateTime.DaysInMonth(anchorDate.Year, anchorDate.Month)))
            : (StartOfWeek(anchorDate), StartOfWeek(anchorDate).AddDays(6));
    }

    private static DateTime StartOfWeek(DateTime date)
    {
        var delta = ((int)date.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return date.AddDays(-delta).Date;
    }

    private static IReadOnlyList<ActivityBreakdownItem> BuildItemsFromEntries(
        IEnumerable<WorkEntry> entries,
        IReadOnlyList<string> colorPalette)
    {
        var grouped = entries
            .GroupBy(entry => NormalizeTaskName(entry.Task))
            .Select(group => new
            {
                Name = group.Key,
                Minutes = (int)Math.Round(group.Sum(item => (item.End - item.Start).TotalMinutes))
            })
            .Where(item => item.Minutes > 0)
            .OrderByDescending(item => item.Minutes)
            .ThenBy(item => item.Name)
            .ToList();

        var totalMinutes = grouped.Sum(item => item.Minutes);
        if (totalMinutes <= 0)
        {
            return [];
        }

        var results = new List<ActivityBreakdownItem>(grouped.Count);
        for (var i = 0; i < grouped.Count; i++)
        {
            var item = grouped[i];
            var color = colorPalette.Count == 0 ? "#7398E6" : colorPalette[i % colorPalette.Count];
            results.Add(new ActivityBreakdownItem
            {
                Name = item.Name,
                ColorHex = color,
                Minutes = item.Minutes,
                Percentage = item.Minutes * 100.0 / totalMinutes
            });
        }

        return results;
    }

    private static IReadOnlyList<PieSliceItem> BuildPieSlices(IReadOnlyList<ActivityBreakdownItem> items)
    {
        if (items.Count == 0)
        {
            return [];
        }

        var slices = new List<PieSliceItem>(items.Count);
        var startAngle = -90d;

        for (var i = 0; i < items.Count; i++)
        {
            var isLast = i == items.Count - 1;
            var requestedSweep = isLast
                ? 360d - (startAngle + 90d)
                : 360d * items[i].Percentage / 100d;
            var sweepAngle = Math.Clamp(requestedSweep, 0d, 360d);

            if (sweepAngle < 0.01d)
            {
                continue;
            }

            slices.Add(new PieSliceItem
            {
                ColorHex = items[i].ColorHex,
                PathData = sweepAngle >= 359.99d
                    ? BuildFullCirclePath()
                    : BuildPieSlicePath(startAngle, sweepAngle)
            });

            startAngle += sweepAngle;
        }

        return slices;
    }

    private static string BuildPieSlicePath(double startAngle, double sweepAngle)
    {
        var startRadians = DegreesToRadians(startAngle);
        var endRadians = DegreesToRadians(startAngle + sweepAngle);

        var startX = PieCenter + (PieRadius * Math.Cos(startRadians));
        var startY = PieCenter + (PieRadius * Math.Sin(startRadians));
        var endX = PieCenter + (PieRadius * Math.Cos(endRadians));
        var endY = PieCenter + (PieRadius * Math.Sin(endRadians));
        var largeArc = sweepAngle > 180d ? 1 : 0;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {PieCenter:0.###},{PieCenter:0.###} L {startX:0.###},{startY:0.###} A {PieRadius:0.###},{PieRadius:0.###} 0 {largeArc} 1 {endX:0.###},{endY:0.###} Z");
    }

    private static string BuildFullCirclePath()
    {
        var topY = PieCenter - PieRadius;
        var bottomY = PieCenter + PieRadius;

        return string.Create(
            CultureInfo.InvariantCulture,
            $"M {PieCenter:0.###},{PieCenter:0.###} L {PieCenter:0.###},{topY:0.###} A {PieRadius:0.###},{PieRadius:0.###} 0 1 1 {PieCenter:0.###},{bottomY:0.###} A {PieRadius:0.###},{PieRadius:0.###} 0 1 1 {PieCenter:0.###},{topY:0.###} Z");
    }

    private static double DegreesToRadians(double angle)
    {
        return angle * Math.PI / 180d;
    }

    private static void SetCollection<T>(ObservableCollection<T> target, IReadOnlyList<T> source)
    {
        target.Clear();
        foreach (var item in source)
        {
            target.Add(item);
        }
    }

    private static string NormalizeTaskName(string task)
    {
        return string.IsNullOrWhiteSpace(task) ? "Uncategorized" : task.Trim();
    }
}
