using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Services;

public interface ITimeLogStorageService
{
    string RecordsDirectoryPath { get; }
    bool RecordsDirectoryExists();
    void CreateRecordsDirectory();
    Task<IReadOnlyList<WorkEntry>> LoadEntriesForDateAsync(DateTime date);
    Task<IReadOnlyList<WorkEntry>> LoadEntriesInRangeAsync(DateTime startDate, DateTime endDateInclusive);
    Task SaveEntriesForDateAsync(DateTime date, IReadOnlyList<WorkEntry> dayEntries);
}
