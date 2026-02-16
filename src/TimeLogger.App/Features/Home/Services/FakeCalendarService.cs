using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Services;

public sealed class FakeCalendarService : ICalendarService
{
    public Task<IReadOnlyList<CalEvent>> GetEventsAsync(DateTimeOffset start, DateTimeOffset endExclusive)
    {
        var day = start.Date;
        var events = new List<CalEvent>
        {
            new()
            {
                Subject = "Team Sync",
                Location = "Online",
                Start = day.AddHours(9),
                End = day.AddHours(9.5)
            },
            new()
            {
                Subject = "Focus Block",
                Location = "Desk",
                Start = day.AddHours(9.75),
                End = day.AddHours(11.25)
            },
            new()
            {
                Subject = "1:1",
                Location = "Teams",
                Start = day.AddHours(13),
                End = day.AddHours(13.5)
            }
        };

        var filtered = events
            .Where(item => item.Start < endExclusive && item.End > start)
            .OrderBy(item => item.Start)
            .ToList();

        return Task.FromResult<IReadOnlyList<CalEvent>>(filtered);
    }
}
