namespace Assist.Services;

using Assist.Models;

/// <summary>
/// Analyzes bridge (köprü) day opportunities for each holiday.
/// A bridge opportunity exists when 1–2 working days separate a holiday block
/// from a weekend (or another holiday), so taking those days off creates a
/// longer uninterrupted stretch of free time.
/// </summary>
internal static class BridgeAnalyzer
{
    // Combinations of (daysBefore, daysAfter) to evaluate
    private static readonly (int Before, int After)[] Combos =
        [(1, 0), (0, 1), (2, 0), (0, 2), (1, 1)];

    // ─── Public API ─────────────────────────────────────────────────────────────

    public static IReadOnlyList<HolidayViewModel> Analyze(
        IReadOnlyList<HolidayEntry> holidays, int year)
    {
        var freeDays = BuildFreeDaySet(holidays, year);
        return [.. holidays.Select(h => BuildViewModel(h, freeDays))];
    }

    // ─── Free-day set ────────────────────────────────────────────────────────────

    private static HashSet<DateOnly> BuildFreeDaySet(
        IReadOnlyList<HolidayEntry> holidays, int year)
    {
        var set = new HashSet<DateOnly>();

        var d   = new DateOnly(year, 1, 1);
        var end = new DateOnly(year, 12, 31);
        while (d <= end)
        {
            if (d.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
                set.Add(d);
            d = d.AddDays(1);
        }

        foreach (var h in holidays)
        {
            var day = h.StartDate;
            while (day <= h.EndDate) { set.Add(day); day = day.AddDays(1); }
        }

        return set;
    }

    // ─── Per-holiday analysis ────────────────────────────────────────────────────

    private static HolidayViewModel BuildViewModel(
        HolidayEntry h, HashSet<DateOnly> freeDays)
    {
        var (blockStart, blockEnd) = GetBlock(h.StartDate, freeDays);
        int baseDays = blockEnd.DayNumber - blockStart.DayNumber + 1;

        var bridges = new List<(int Leave, int Total)>();

        foreach (var (before, after) in Combos)
        {
            var result = TryBridge(blockStart, blockEnd, freeDays, before, after);
            if (result.HasValue && result.Value.Total > baseDays)
                bridges.Add(result.Value);
        }

        // Keep best total-days per leave-day count
        var deduped = bridges
            .GroupBy(b => b.Leave)
            .Select(g => g.MaxBy(b => b.Total))
            .OrderBy(b => b!.Leave)
            .ToList();

        bool hasBridge;
        string summary;
        BridgeQuality quality;

        if (deduped.Count == 0)
        {
            hasBridge = false;
            if (baseDays > h.DayCount)
            {
                summary = "Zaten birleşiyor";
                quality = BridgeQuality.AlreadyConnected;
            }
            else
            {
                summary = "Fırsat yok";
                quality = BridgeQuality.None;
            }
        }
        else
        {
            hasBridge = true;
            var best = deduped
                .OrderBy(b => b!.Leave)
                .ThenByDescending(b => b!.Total)
                .First()!;

            summary = $"{best.Leave} gün izin → {best.Total} gün tatil";
            quality = best switch
            {
                { Leave: 1, Total: >= 5 } => BridgeQuality.Best,
                { Leave: 1 }              => BridgeQuality.Good,
                { Leave: 2, Total: >= 7 } => BridgeQuality.Good,
                _                         => BridgeQuality.Weak,
            };
        }

        return new HolidayViewModel(
            h.Name,
            h.Type,
            h.StartDate,
            h.EndDate,
            h.DayCount,
            GetTurkishDayName(h.StartDate.DayOfWeek),
            h.StartDate.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday,
            hasBridge,
            summary,
            quality);
    }

    // ─── Bridge attempt ──────────────────────────────────────────────────────────

    /// <summary>
    /// Tries to fill a gap of <paramref name="beforeN"/> work days before the block
    /// and/or <paramref name="afterN"/> work days after the block.
    /// Returns (leaveCount, newTotalDays) on success, null otherwise.
    /// </summary>
    private static (int Leave, int Total)? TryBridge(
        DateOnly blockStart, DateOnly blockEnd,
        HashSet<DateOnly> freeDays, int beforeN, int afterN)
    {
        var extra = new HashSet<DateOnly>();

        if (beforeN > 0 && !TryFillGap(blockStart, freeDays, beforeN, forward: false, extra))
            return null;

        if (afterN > 0 && !TryFillGap(blockEnd, freeDays, afterN, forward: true, extra))
            return null;

        if (extra.Count == 0) return null;

        var tempFree = new HashSet<DateOnly>(freeDays);
        foreach (var d in extra) tempFree.Add(d);

        var (newStart, newEnd) = GetBlock(blockStart, tempFree);
        return (extra.Count, newEnd.DayNumber - newStart.DayNumber + 1);
    }

    /// <summary>
    /// Checks whether exactly <paramref name="n"/> consecutive work days form a
    /// gap between the block boundary and the next free day in the given direction.
    /// On success, adds those gap days to <paramref name="result"/>.
    /// </summary>
    private static bool TryFillGap(
        DateOnly boundary, HashSet<DateOnly> freeDays,
        int n, bool forward, HashSet<DateOnly> result)
    {
        int step = forward ? 1 : -1;
        var cur  = boundary.AddDays(step);
        var gap  = new List<DateOnly>(n);

        for (int i = 0; i < n; i++)
        {
            if (freeDays.Contains(cur)) return false; // hit a free day too early
            gap.Add(cur);
            cur = cur.AddDays(step);
        }

        // There must be a free day immediately on the far side of the gap
        if (!freeDays.Contains(cur)) return false;

        foreach (var d in gap) result.Add(d);
        return true;
    }

    // ─── Block helper ────────────────────────────────────────────────────────────

    private static (DateOnly Start, DateOnly End) GetBlock(
        DateOnly anchor, HashSet<DateOnly> freeDays)
    {
        var start = anchor;
        while (freeDays.Contains(start.AddDays(-1))) start = start.AddDays(-1);

        var end = anchor;
        while (freeDays.Contains(end.AddDays(1))) end = end.AddDays(1);

        return (start, end);
    }

    // ─── Localisation ────────────────────────────────────────────────────────────

    private static string GetTurkishDayName(DayOfWeek dow) => dow switch
    {
        DayOfWeek.Monday    => "Pazartesi",
        DayOfWeek.Tuesday   => "Salı",
        DayOfWeek.Wednesday => "Çarşamba",
        DayOfWeek.Thursday  => "Perşembe",
        DayOfWeek.Friday    => "Cuma",
        DayOfWeek.Saturday  => "Cumartesi",
        DayOfWeek.Sunday    => "Pazar",
        _                   => string.Empty,
    };
}
