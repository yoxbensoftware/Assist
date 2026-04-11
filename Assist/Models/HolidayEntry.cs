namespace Assist.Models;

internal enum HolidayType { Official, Religious, HalfDay }

internal enum BridgeQuality { Best, Good, Weak, AlreadyConnected, None }

/// <summary>Raw holiday data returned by the provider.</summary>
internal sealed record HolidayEntry(
    string Name,
    HolidayType Type,
    DateOnly StartDate,
    DateOnly EndDate)
{
    public int DayCount => EndDate.DayNumber - StartDate.DayNumber + 1;
}

/// <summary>Flat view-model used to populate the holiday grid.</summary>
internal sealed record HolidayViewModel(
    string Name,
    HolidayType Type,
    DateOnly StartDate,
    DateOnly EndDate,
    int DayCount,
    string WeekDay,
    bool IsWeekend,
    bool HasBridge,
    string BridgeSummary,
    BridgeQuality BridgeQuality);
