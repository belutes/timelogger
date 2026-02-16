namespace TimeLogger.App.Features.DataAnalysis.Models;

public sealed class ActivityBreakdownItem
{
    public required string Name { get; init; }
    public required string ColorHex { get; init; }
    public required int Minutes { get; init; }
    public required double Percentage { get; init; }

    public string DurationText => $"{Minutes / 60}h {Minutes % 60:00}m";
    public string PercentText => $"{Percentage:0}%";
    public string LegendText => $"{Name} ({Percentage:0}%)";
}
