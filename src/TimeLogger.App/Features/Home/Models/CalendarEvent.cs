namespace TimeLogger.App.Features.Home.Models;

public sealed class CalendarEvent
{
    public required string Title { get; init; }
    public required string DateLine { get; init; }
    public required string TimeRange { get; init; }
    public required string Location { get; init; }
}
