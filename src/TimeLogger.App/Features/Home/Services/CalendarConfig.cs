namespace TimeLogger.App.Features.Home.Services;

public sealed class CalendarConfig
{
    public string Provider { get; init; } = "Graph";
    public string ClientId { get; init; } = string.Empty;
    public string TenantId { get; init; } = "common";
    public string RedirectUri { get; init; } = "http://localhost";
    public string TimeZoneId { get; init; } = string.Empty;
}
