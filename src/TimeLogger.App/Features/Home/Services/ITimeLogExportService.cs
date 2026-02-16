using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Services;

public interface ITimeLogExportService
{
    Task<string> ExportDayCsvAsync(
        string recordsDirectoryPath,
        DateTime selectedDate,
        IReadOnlyList<WorkEntry> entries,
        IReadOnlyList<CalEvent> calendarEvents,
        string totalDurationText,
        string? calendarFetchError);

    Task<string> ExportRangeCsvAsync(
        string recordsDirectoryPath,
        DateTime startDate,
        DateTime endDateInclusive,
        IReadOnlyList<WorkEntry> entries,
        IReadOnlyList<CalEvent> calendarEvents,
        string? calendarFetchError);
}
