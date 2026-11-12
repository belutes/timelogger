using System;

namespace TimeLogger.App.Features.Home.Models;

public sealed class CalEvent
{
    public string? Id { get; init; }
    public required string Subject { get; init; }
    public required string Location { get; init; }
    public required DateTimeOffset Start { get; init; }
    public required DateTimeOffset End { get; init; }

    public string Organizer { get; init; } = "Not provided";
    public string Attendees { get; init; } = "Not provided";
    public string Description { get; init; } = "Not provided";
    public string Status { get; init; } = "Not provided";
    public string Response { get; init; } = "Not provided";
    public string Importance { get; init; } = "Not provided";
    public string Categories { get; init; } = "None";
    public bool IsAllDay { get; init; }
    public bool IsCancelled { get; init; }
    public string? JoinUrl { get; init; }
    public string? WebLink { get; init; }

    public string TimeRange => $"{Start:hh:mm}-{End:hh:mm tt}";
    public string DateLine => Start.ToString("dddd, MMMM d, yyyy");
}
