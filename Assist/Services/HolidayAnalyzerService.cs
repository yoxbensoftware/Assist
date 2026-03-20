using Assist.Models;

namespace Assist.Services;

/// <summary>
/// Provides public holiday data and sprint risk analysis.
/// Holidays are hardcoded for 2026 (Turkey + Germany).
/// </summary>
public sealed class HolidayAnalyzerService
{
    private static readonly List<HolidayEntry> Holidays2026 =
    [
        H("2026-01-01", "DE/TR", "Yılbaşı / New Year"),
        H("2026-01-06", "DE",    "Epiphany"),
        H("2026-03-19", "TR",    "Ramazan Bayramı Arife"),
        H("2026-03-20", "TR",    "Ramazan Bayramı 1. Gün"),
        H("2026-03-21", "TR",    "Ramazan Bayramı 2. Gün"),
        H("2026-03-22", "TR",    "Ramazan Bayramı 3. Gün"),
        H("2026-04-03", "DE",    "Good Friday"),
        H("2026-04-06", "DE",    "Easter Monday"),
        H("2026-04-23", "TR",    "Ulusal Egemenlik ve Çocuk Bayramı"),
        H("2026-05-01", "DE/TR", "İşçi Bayramı / Labour Day"),
        H("2026-05-14", "DE",    "Ascension Day"),
        H("2026-05-19", "TR",    "Atatürk'ü Anma, Gençlik ve Spor Bayramı"),
        H("2026-05-25", "DE",    "Whit Monday"),
        H("2026-05-26", "TR",    "Kurban Bayramı Arife"),
        H("2026-05-27", "TR",    "Kurban Bayramı 1. Gün"),
        H("2026-05-28", "TR",    "Kurban Bayramı 2. Gün"),
        H("2026-05-29", "TR",    "Kurban Bayramı 3. Gün"),
        H("2026-05-30", "TR",    "Kurban Bayramı 4. Gün"),
        H("2026-06-04", "DE",    "Corpus Christi"),
        H("2026-07-15", "TR",    "Demokrasi ve Millî Birlik Günü"),
        H("2026-08-15", "DE",    "Assumption Day"),
        H("2026-08-30", "TR",    "Zafer Bayramı"),
        H("2026-10-03", "DE",    "German Unity Day"),
        H("2026-10-28", "TR",    "Cumhuriyet Bayramı Arife"),
        H("2026-10-29", "TR",    "Cumhuriyet Bayramı"),
        H("2026-10-31", "DE",    "Reformation Day"),
        H("2026-11-01", "DE",    "All Saints' Day"),
        H("2026-11-18", "DE",    "Repentance Day"),
        H("2026-12-25", "DE",    "Christmas Day"),
        H("2026-12-26", "DE",    "Christmas Day 2")
    ];

    /// <summary>
    /// Analyzes holidays within a sprint date range and calculates risk.
    /// </summary>
    public SprintAnalysisResult Analyze(DateTime sprintStart, DateTime sprintEnd)
    {
        var filtered = Holidays2026
            .Where(h => h.Date >= sprintStart.Date && h.Date <= sprintEnd.Date)
            .OrderBy(h => h.Date)
            .ToList();

        var weekdayDates = new HashSet<DateTime>(
            filtered.Where(h => !h.IsWeekend).Select(h => h.Date));

        var analyzed = filtered
            .Select(h => new AnalyzedHoliday
            {
                Holiday = h,
                Impact = CalculateImpact(h, weekdayDates),
                IsCluster = IsPartOfCluster(h.Date, weekdayDates)
            })
            .ToList();

        int weekdayCount = analyzed.Count(a => !a.Holiday.IsWeekend);
        int weekendCount = analyzed.Count(a => a.Holiday.IsWeekend);
        int sprintTotalDays = (int)(sprintEnd.Date - sprintStart.Date).TotalDays + 1;
        int sprintWorkDays = CountWorkDays(sprintStart.Date, sprintEnd.Date);
        double capacityLoss = sprintWorkDays > 0
            ? Math.Round(weekdayCount * 100.0 / sprintWorkDays, 1)
            : 0;

        var risk = ClassifyRisk(weekdayCount);

        return new SprintAnalysisResult
        {
            Holidays = analyzed,
            TotalHolidayCount = analyzed.Count,
            WeekdayHolidayCount = weekdayCount,
            WeekendHolidayCount = weekendCount,
            SprintTotalDays = sprintTotalDays,
            SprintWorkDays = sprintWorkDays,
            CapacityLossPercent = capacityLoss,
            Risk = risk,
            RiskMessage = BuildRiskMessage(risk, weekdayCount, capacityLoss)
        };
    }

    private static ImpactLevel CalculateImpact(HolidayEntry holiday, HashSet<DateTime> weekdayHolidayDates)
    {
        if (holiday.IsWeekend)
            return ImpactLevel.Low;

        if (IsPartOfCluster(holiday.Date, weekdayHolidayDates))
            return ImpactLevel.High;

        // Friday or Monday adjacent to weekend — extended weekend effect
        if (holiday.Date.DayOfWeek is DayOfWeek.Friday or DayOfWeek.Monday)
            return ImpactLevel.Medium;

        return ImpactLevel.High;
    }

    private static bool IsPartOfCluster(DateTime date, HashSet<DateTime> weekdayDates)
    {
        return weekdayDates.Contains(date.AddDays(-1)) || weekdayDates.Contains(date.AddDays(1));
    }

    private static RiskLevel ClassifyRisk(int weekdayHolidayCount) => weekdayHolidayCount switch
    {
        >= 3 => RiskLevel.High,
        >= 1 => RiskLevel.Medium,
        _ => RiskLevel.Low
    };

    private static string BuildRiskMessage(RiskLevel risk, int weekdayCount, double capacityLoss) => risk switch
    {
        RiskLevel.High => $"⚠️ Yüksek Risk: Sprint {weekdayCount} iş günü tatili ile çakışıyor (kapasite kaybı: %{capacityLoss:F1})",
        RiskLevel.Medium => $"⚡ Orta Risk: Sprint {weekdayCount} iş günü tatili içeriyor (kapasite kaybı: %{capacityLoss:F1})",
        _ => "✅ Düşük Risk: Sprint'te iş gününe denk gelen tatil yok"
    };

    private static int CountWorkDays(DateTime start, DateTime end)
    {
        int count = 0;
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday)
                count++;
        }
        return count;
    }

    private static HolidayEntry H(string date, string country, string name) => new()
    {
        Date = DateTime.Parse(date),
        Country = country,
        Name = name
    };
}
