using System;

namespace TimeLogger.App.Features.Home.Models;

public sealed class CalEvent
{
    public required string Subject { get; init; }
    public required string Location { get; init; }
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }

    public string TimeRange => $"{Start:hh:mm}-{End:hh:mm tt}";
    public string DateLine => Start.ToString("dddd, MMMM d, yyyy");
}
