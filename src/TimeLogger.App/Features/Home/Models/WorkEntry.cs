using System;
using System.Text.Json.Serialization;

namespace TimeLogger.App.Features.Home.Models;

public sealed class WorkEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public DateTime Date { get; set; }
    public TimeSpan Start { get; set; }
    public TimeSpan End { get; set; }
    public string Task { get; set; } = string.Empty;
    public string Notes { get; set; } = string.Empty;

    [JsonIgnore]
    public string DateLabel => Date.ToString("MM/dd/yy");

    [JsonIgnore]
    public string TimeRange => $"{DateTime.Today.Add(Start):hh:mm tt} - {DateTime.Today.Add(End):hh:mm tt}";
}
