using System;

namespace TimeLogger.App.Features.Home.Models;

public sealed class DateRangeInput
{
    public required DateTime StartDate { get; init; }
    public required DateTime EndDate { get; init; }
}
