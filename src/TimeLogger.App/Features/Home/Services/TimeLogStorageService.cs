using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Services;

public sealed class TimeLogStorageService : ITimeLogStorageService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public string RecordsDirectoryPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
        "Time Log Records");

    public bool RecordsDirectoryExists() => Directory.Exists(RecordsDirectoryPath);

    public void CreateRecordsDirectory()
    {
        Directory.CreateDirectory(RecordsDirectoryPath);
    }

    public async Task<IReadOnlyList<WorkEntry>> LoadEntriesForDateAsync(DateTime date)
    {
        var monthFile = GetMonthFilePath(date);
        if (!File.Exists(monthFile))
        {
            return Array.Empty<WorkEntry>();
        }

        var record = await ReadMonthRecordAsync(monthFile);
        return record.Entries
            .Where(entry => entry.Date.Date == date.Date)
            .OrderBy(entry => entry.Start)
            .ToList();
    }

    public async Task<IReadOnlyList<WorkEntry>> LoadEntriesInRangeAsync(DateTime startDate, DateTime endDateInclusive)
    {
        var start = startDate.Date;
        var end = endDateInclusive.Date;
        if (end < start)
        {
            return Array.Empty<WorkEntry>();
        }

        var rangeEntries = new List<WorkEntry>();
        var monthCursor = new DateTime(start.Year, start.Month, 1);
        var lastMonth = new DateTime(end.Year, end.Month, 1);

        while (monthCursor <= lastMonth)
        {
            var monthFile = GetMonthFilePath(monthCursor);
            if (File.Exists(monthFile))
            {
                var monthRecord = await ReadMonthRecordAsync(monthFile);
                rangeEntries.AddRange(monthRecord.Entries.Where(entry =>
                    entry.Date.Date >= start && entry.Date.Date <= end));
            }

            monthCursor = monthCursor.AddMonths(1);
        }

        return rangeEntries
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Start)
            .ToList();
    }

    public async Task SaveEntriesForDateAsync(DateTime date, IReadOnlyList<WorkEntry> dayEntries)
    {
        var monthFile = GetMonthFilePath(date);
        var monthRecord = File.Exists(monthFile)
            ? await ReadMonthRecordAsync(monthFile)
            : new MonthRecord();

        monthRecord.Entries = monthRecord.Entries
            .Where(entry => entry.Date.Date != date.Date)
            .ToList();

        monthRecord.Entries.AddRange(dayEntries.Select(CloneEntry));
        monthRecord.Entries = monthRecord.Entries
            .OrderBy(entry => entry.Date)
            .ThenBy(entry => entry.Start)
            .ToList();

        await using var stream = File.Create(monthFile);
        await JsonSerializer.SerializeAsync(stream, monthRecord, JsonOptions);
    }

    private async Task<MonthRecord> ReadMonthRecordAsync(string monthFile)
    {
        await using var stream = File.OpenRead(monthFile);
        var record = await JsonSerializer.DeserializeAsync<MonthRecord>(stream, JsonOptions);
        return record ?? new MonthRecord();
    }

    private string GetMonthFilePath(DateTime date)
    {
        return Path.Combine(RecordsDirectoryPath, $"{date:yyyy-MM}.json");
    }

    private static WorkEntry CloneEntry(WorkEntry entry)
    {
        return new WorkEntry
        {
            Id = entry.Id,
            Date = entry.Date,
            Start = entry.Start,
            End = entry.End,
            Task = entry.Task,
            Notes = entry.Notes
        };
    }

    private sealed class MonthRecord
    {
        public List<WorkEntry> Entries { get; set; } = [];
    }
}
