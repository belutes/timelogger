using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TimeLogger.App.Features.Home.Models;

namespace TimeLogger.App.Features.Home.Services;

public interface ICalendarService
{
    Task<IReadOnlyList<CalEvent>> GetEventsAsync(DateTimeOffset start, DateTimeOffset endExclusive);
}
