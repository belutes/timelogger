namespace TimeLogger.App.Features.Home.Models;

public sealed class EditEntryInput
{
    public string TaskName { get; init; } = string.Empty;
    public string Notes { get; init; } = string.Empty;
    public string StartTime { get; init; } = string.Empty;
    public string EndTime { get; init; } = string.Empty;
}
