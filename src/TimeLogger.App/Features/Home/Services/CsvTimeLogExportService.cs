using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Services;

public sealed class CsvTimeLogExportService : ITimeLogExportService
{
    public async Task<string> ExportDayCsvAsync(
        string recordsDirectoryPath,
        DateTime selectedDate,
        IReadOnlyList<WorkEntry> entries,
        IReadOnlyList<CalEvent> calendarEvents,
        string totalDurationText,
        string? calendarFetchError)
    {
        var sortedEntries = entries.OrderBy(item => item.Start).ToList();
        var sortedEvents = calendarEvents.OrderBy(item => item.Start).ToList();

        var exportDirectory = Path.Combine(recordsDirectoryPath, "Exports", selectedDate.ToString("yyyy", CultureInfo.InvariantCulture), selectedDate.ToString("MM", CultureInfo.InvariantCulture));
        Directory.CreateDirectory(exportDirectory);

        var fileName = $"timelog-day-{selectedDate:yyyy-MM-dd}-{DateTime.Now:HHmmss}.csv";
        var filePath = Path.Combine(exportDirectory, fileName);

        var csv = new StringBuilder();

        AppendRow(csv, "ReportType", "DayExport");
        AppendRow(csv, "SelectedDate", selectedDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AppendRow(csv, "SelectedDayOfWeek", selectedDate.ToString("dddd", CultureInfo.InvariantCulture));
        AppendRow(csv, "ExportedAtLocal", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        AppendRow(csv, "EntryCount", sortedEntries.Count.ToString(CultureInfo.InvariantCulture));
        AppendRow(csv, "CalendarEventCount", sortedEvents.Count.ToString(CultureInfo.InvariantCulture));
        AppendRow(csv, "TotalDuration", totalDurationText);
        AppendRow(csv, "CalendarFetchStatus", string.IsNullOrWhiteSpace(calendarFetchError) ? "Loaded" : "Fallback");
        if (!string.IsNullOrWhiteSpace(calendarFetchError))
        {
            AppendRow(csv, "CalendarFetchError", calendarFetchError);
        }

        csv.AppendLine();
        csv.AppendLine("Entries");
        csv.AppendLine("EntryId,Date,DayOfWeek,StartTime,EndTime,DurationMinutes,Task,Notes,OverlapsAnotherEntry");

        foreach (var entry in sortedEntries)
        {
            var durationMinutes = (entry.End - entry.Start).TotalMinutes;
            var overlaps = sortedEntries.Any(other =>
                other.Id != entry.Id && entry.Start < other.End && other.Start < entry.End);

            AppendRow(csv,
                entry.Id.ToString(),
                entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                entry.Date.ToString("dddd", CultureInfo.InvariantCulture),
                DateTime.Today.Add(entry.Start).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                DateTime.Today.Add(entry.End).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                durationMinutes.ToString("0", CultureInfo.InvariantCulture),
                entry.Task,
                entry.Notes,
                overlaps ? "Yes" : "No");
        }

        if (sortedEntries.Count == 0)
        {
            AppendRow(csv, "", "", "", "", "", "", "No entries", "", "");
        }

        csv.AppendLine();
        csv.AppendLine("CalendarEvents");
        csv.AppendLine("Subject,Location,Date,StartTime,EndTime,DurationMinutes,TimeRange");

        foreach (var calendarEvent in sortedEvents)
        {
            var durationMinutes = (calendarEvent.End - calendarEvent.Start).TotalMinutes;
            AppendRow(csv,
                calendarEvent.Subject,
                calendarEvent.Location,
                calendarEvent.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                calendarEvent.Start.ToString("hh:mm tt", CultureInfo.InvariantCulture),
                calendarEvent.End.ToString("hh:mm tt", CultureInfo.InvariantCulture),
                durationMinutes.ToString("0", CultureInfo.InvariantCulture),
                calendarEvent.TimeRange);
        }

        if (sortedEvents.Count == 0)
        {
            AppendRow(csv, "No calendar events", "", "", "", "", "", "");
        }

        await File.WriteAllTextAsync(filePath, csv.ToString());
        return filePath;
    }

    public async Task<string> ExportRangeCsvAsync(
        string recordsDirectoryPath,
        DateTime startDate,
        DateTime endDateInclusive,
        IReadOnlyList<WorkEntry> entries,
        IReadOnlyList<CalEvent> calendarEvents,
        string? calendarFetchError)
    {
        var start = startDate.Date;
        var end = endDateInclusive.Date;
        var sortedEntries = entries
            .OrderBy(item => item.Date)
            .ThenBy(item => item.Start)
            .ToList();
        var sortedEvents = calendarEvents.OrderBy(item => item.Start).ToList();

        var exportDirectory = Path.Combine(recordsDirectoryPath, "Exports", "Range", $"{start:yyyy-MM}-{end:yyyy-MM}");
        Directory.CreateDirectory(exportDirectory);

        var fileName = $"timelog-range-{start:yyyy-MM-dd}_to_{end:yyyy-MM-dd}-{DateTime.Now:HHmmss}.csv";
        var filePath = Path.Combine(exportDirectory, fileName);

        var totalMinutes = sortedEntries.Sum(item => (item.End - item.Start).TotalMinutes);
        var totalDuration = TimeSpan.FromMinutes(totalMinutes);
        var totalDurationText = $"{(int)totalDuration.TotalHours}h {totalDuration.Minutes}m";
        var dayCount = (end - start).Days + 1;

        var csv = new StringBuilder();

        AppendRow(csv, "ReportType", "RangeExport");
        AppendRow(csv, "RangeStartDate", start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AppendRow(csv, "RangeEndDateInclusive", end.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));
        AppendRow(csv, "RangeDays", dayCount.ToString(CultureInfo.InvariantCulture));
        AppendRow(csv, "ExportedAtLocal", DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        AppendRow(csv, "EntryCount", sortedEntries.Count.ToString(CultureInfo.InvariantCulture));
        AppendRow(csv, "CalendarEventCount", sortedEvents.Count.ToString(CultureInfo.InvariantCulture));
        AppendRow(csv, "TotalDuration", totalDurationText);
        AppendRow(csv, "CalendarFetchStatus", string.IsNullOrWhiteSpace(calendarFetchError) ? "Loaded" : "Fallback");
        if (!string.IsNullOrWhiteSpace(calendarFetchError))
        {
            AppendRow(csv, "CalendarFetchError", calendarFetchError);
        }

        csv.AppendLine();
        csv.AppendLine("DaySummaries");
        csv.AppendLine("Date,DayOfWeek,EntryCount,TotalMinutes,FirstStart,LastEnd");

        var daySummaries = sortedEntries
            .GroupBy(item => item.Date.Date)
            .OrderBy(group => group.Key);

        foreach (var dayGroup in daySummaries)
        {
            var minutes = dayGroup.Sum(item => (item.End - item.Start).TotalMinutes);
            var firstStart = dayGroup.Min(item => item.Start);
            var lastEnd = dayGroup.Max(item => item.End);
            AppendRow(csv,
                dayGroup.Key.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                dayGroup.Key.ToString("dddd", CultureInfo.InvariantCulture),
                dayGroup.Count().ToString(CultureInfo.InvariantCulture),
                minutes.ToString("0", CultureInfo.InvariantCulture),
                DateTime.Today.Add(firstStart).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                DateTime.Today.Add(lastEnd).ToString("hh:mm tt", CultureInfo.InvariantCulture));
        }

        if (sortedEntries.Count == 0)
        {
            AppendRow(csv, "", "", "0", "0", "", "");
        }

        csv.AppendLine();
        csv.AppendLine("Entries");
        csv.AppendLine("EntryId,Date,DayOfWeek,StartTime,EndTime,DurationMinutes,Task,Notes,OverlapsAnotherEntryOnSameDay");

        foreach (var entry in sortedEntries)
        {
            var durationMinutes = (entry.End - entry.Start).TotalMinutes;
            var overlaps = sortedEntries.Any(other =>
                other.Id != entry.Id &&
                other.Date.Date == entry.Date.Date &&
                entry.Start < other.End &&
                other.Start < entry.End);

            AppendRow(csv,
                entry.Id.ToString(),
                entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                entry.Date.ToString("dddd", CultureInfo.InvariantCulture),
                DateTime.Today.Add(entry.Start).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                DateTime.Today.Add(entry.End).ToString("hh:mm tt", CultureInfo.InvariantCulture),
                durationMinutes.ToString("0", CultureInfo.InvariantCulture),
                entry.Task,
                entry.Notes,
                overlaps ? "Yes" : "No");
        }

        if (sortedEntries.Count == 0)
        {
            AppendRow(csv, "", "", "", "", "", "", "No entries in selected range", "", "");
        }

        csv.AppendLine();
        csv.AppendLine("CalendarEvents");
        csv.AppendLine("Subject,Location,Date,StartTime,EndTime,DurationMinutes,TimeRange");

        foreach (var calendarEvent in sortedEvents)
        {
            var durationMinutes = (calendarEvent.End - calendarEvent.Start).TotalMinutes;
            AppendRow(csv,
                calendarEvent.Subject,
                calendarEvent.Location,
                calendarEvent.Start.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                calendarEvent.Start.ToString("hh:mm tt", CultureInfo.InvariantCulture),
                calendarEvent.End.ToString("hh:mm tt", CultureInfo.InvariantCulture),
                durationMinutes.ToString("0", CultureInfo.InvariantCulture),
                calendarEvent.TimeRange);
        }

        if (sortedEvents.Count == 0)
        {
            AppendRow(csv, "No calendar events", "", "", "", "", "", "");
        }

        await File.WriteAllTextAsync(filePath, csv.ToString());
        return filePath;
    }

    private static void AppendRow(StringBuilder csv, params string[] values)
    {
        csv.AppendLine(string.Join(',', values.Select(Escape)));
    }

    private static string Escape(string value)
    {
        var safe = value.Replace("\r", " ").Replace("\n", " ");
        if (safe.Contains(',') || safe.Contains('"'))
        {
            return $"\"{safe.Replace("\"", "\"\"")}\"";
        }

        return safe;
    }
}
