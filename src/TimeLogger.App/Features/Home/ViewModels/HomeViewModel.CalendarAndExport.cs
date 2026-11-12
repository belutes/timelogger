using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.ViewModels;

public sealed partial class HomeViewModel
{
    private async Task LoadEntriesForSelectedDateAsync()
    {
        var version = ++_entriesLoadVersion;
        _entriesReady = false;
        AddEntryCommand.NotifyCanExecuteChanged();
        DeleteSelectedCommand.NotifyCanExecuteChanged();
        EditSelectedEntryCommand.NotifyCanExecuteChanged();
        var date = (SelectedDate ?? DateTime.Today).Date;
        WorkEntries.Clear();
        SelectedEntry = null;
        RecalculateTotalDuration();
        OnPropertyChanged(nameof(EntryCountLabel));
        try
        {
            var loadedEntries = await _storage.LoadEntriesForDateAsync(date);
            if (version != _entriesLoadVersion) return;
            foreach (var entry in loadedEntries.OrderBy(item => item.Start)) WorkEntries.Add(entry);
            RecalculateTotalDuration();
            OnPropertyChanged(nameof(EntryCountLabel));
            _entriesReady = true;
        }
        catch (Exception ex)
        {
            if (version == _entriesLoadVersion && _dialogs is not null)
                await _dialogs.ShowAlertAsync("Could not load time entries", $"Entries cannot be changed until this day's records can be loaded. {ex.Message}");
        }
        finally
        {
            if (version == _entriesLoadVersion)
            {
                AddEntryCommand.NotifyCanExecuteChanged();
                DeleteSelectedCommand.NotifyCanExecuteChanged();
                EditSelectedEntryCommand.NotifyCanExecuteChanged();
            }
        }
    }

    private async Task LoadCalendarEventsAsync()
    {
        if (_dialogs is null)
        {
            return;
        }

        var version = ++_calendarLoadVersion;
        CalendarEvents.Clear();
        OnPropertyChanged(nameof(EventCountLabel));
        try
        {
            var (start, endExclusive) = ResolveCalendarRange();
            var events = await _calendarService.GetEventsAsync(start, endExclusive);

            if (version != _calendarLoadVersion) return;
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
            if (version != _calendarLoadVersion) return;
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
            "Week" => (StartOfWeek(day), StartOfWeek(day).AddDays(7)),
            "Month" =>
                (new DateTimeOffset(new DateTime(day.Year, day.Month, 1)), new DateTimeOffset(new DateTime(day.Year, day.Month, 1).AddMonths(1))),
            _ => (new DateTimeOffset(day), new DateTimeOffset(day.AddDays(1)))
        };
    }

    private static DateTimeOffset StartOfWeek(DateTime day)
    {
        var delta = ((int)day.DayOfWeek - (int)DayOfWeek.Monday + 7) % 7;
        return new DateTimeOffset(day.AddDays(-delta));
    }
}
