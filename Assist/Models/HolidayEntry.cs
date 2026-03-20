namespace Assist.Models;

/// <summary>
/// Represents a public holiday entry for sprint risk analysis.
/// </summary>
public sealed class HolidayEntry
{
    public required DateTime Date { get; init; }
    public required string Country { get; init; }
    public required string Name { get; init; }

    public string DayName => Date.ToString("dddd", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
    public bool IsWeekend => Date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday;
}

/// <summary>
/// Analyzed holiday with calculated impact level.
/// </summary>
public sealed class AnalyzedHoliday
{
    public required HolidayEntry Holiday { get; init; }
    public required ImpactLevel Impact { get; init; }
    public required bool IsCluster { get; init; }
}

/// <summary>
/// Impact level of a holiday on sprint capacity.
/// </summary>
public enum ImpactLevel
{
    Low,
    Medium,
    High
}

/// <summary>
/// Risk level classification for overall sprint.
/// </summary>
public enum RiskLevel
{
    Low,
    Medium,
    High
}

/// <summary>
/// Summary of sprint holiday risk analysis.
/// </summary>
public sealed class SprintAnalysisResult
{
    public required List<AnalyzedHoliday> Holidays { get; init; }
    public required int TotalHolidayCount { get; init; }
    public required int WeekdayHolidayCount { get; init; }
    public required int WeekendHolidayCount { get; init; }
    public required int SprintTotalDays { get; init; }
    public required int SprintWorkDays { get; init; }
    public required double CapacityLossPercent { get; init; }
    public required RiskLevel Risk { get; init; }
    public required string RiskMessage { get; init; }
}
