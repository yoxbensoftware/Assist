namespace Assist.Services;

using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Assist.Models;

/// <summary>
/// Fetches Turkish public holidays from the nager.at API with a built-in offline fallback.
/// Results are cached per year so the network is hit at most once per session.
/// </summary>
internal static class TurkishHolidayProvider
{
    private const string ApiUrl = "https://date.nager.at/api/v3/PublicHolidays/{0}/TR";

    private static readonly Dictionary<int, IReadOnlyList<HolidayEntry>> _cache = [];

    // ─── Public API ─────────────────────────────────────────────────────────────

    public static async Task<IReadOnlyList<HolidayEntry>> GetHolidaysAsync(
        int year, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(year, out var cached))
            return cached;

        IReadOnlyList<HolidayEntry> result;
        try
        {
            result = await FetchOnlineAsync(year, ct).ConfigureAwait(false);
        }
        catch
        {
            result = GetBuiltInHolidays(year);
        }

        _cache[year] = result;
        return result;
    }

    public static void InvalidateCache(int year) => _cache.Remove(year);

    // ─── Online ─────────────────────────────────────────────────────────────────

    private static async Task<IReadOnlyList<HolidayEntry>> FetchOnlineAsync(
        int year, CancellationToken ct)
    {
        var url = string.Format(ApiUrl, year);
        var items = await AppConstants.SharedHttpClient
            .GetFromJsonAsync<NagerHolidayDto[]>(url, ct)
            .ConfigureAwait(false);

        if (items is null || items.Length == 0)
            return GetBuiltInHolidays(year);

        // nager.at returns one row per day for multi-day holidays → merge them
        var merged = new List<HolidayEntry>();
        var ordered = items.OrderBy(x => x.Date).ToArray();

        int i = 0;
        while (i < ordered.Length)
        {
            var cur = ordered[i];
            var endDate = cur.Date;

            while (i + 1 < ordered.Length
                && ordered[i + 1].LocalName == cur.LocalName
                && ordered[i + 1].Date.Date == endDate.AddDays(1).Date)
            {
                i++;
                endDate = ordered[i].Date;
            }

            merged.Add(new HolidayEntry(
                cur.LocalName,
                ClassifyType(cur.LocalName),
                DateOnly.FromDateTime(cur.Date),
                DateOnly.FromDateTime(endDate)));
            i++;
        }

        AddHalfDay(year, merged);
        return [.. merged.OrderBy(h => h.StartDate)];
    }

    private static HolidayType ClassifyType(string name) =>
        name.Contains("Ramazan",  StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Kurban",   StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Eid",      StringComparison.OrdinalIgnoreCase) ||
        name.Contains("Arefe",    StringComparison.OrdinalIgnoreCase)
            ? HolidayType.Religious
            : HolidayType.Official;

    // October 28 afternoon is an officially designated half-day in Turkey
    private static void AddHalfDay(int year, List<HolidayEntry> list)
    {
        var oct28 = new DateOnly(year, 10, 28);
        if (oct28.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday) return;
        if (list.Any(h => h.StartDate == oct28)) return;
        list.Add(new HolidayEntry(
            "Cumhuriyet Bayramı Arifesi (Yarım Gün)",
            HolidayType.HalfDay, oct28, oct28));
    }

    // ─── Built-in fallback ───────────────────────────────────────────────────────

    private static IReadOnlyList<HolidayEntry> GetBuiltInHolidays(int year)
    {
        var list = new List<HolidayEntry>
        {
            new("Yeni Yıl Günü",                            HolidayType.Official, D(year,  1,  1), D(year,  1,  1)),
            new("Ulusal Egemenlik ve Çocuk Bayramı",        HolidayType.Official, D(year,  4, 23), D(year,  4, 23)),
            new("Emek ve Dayanışma Günü",                   HolidayType.Official, D(year,  5,  1), D(year,  5,  1)),
            new("Atatürk'ü Anma, Gençlik ve Spor Bayramı", HolidayType.Official, D(year,  5, 19), D(year,  5, 19)),
            new("Demokrasi ve Milli Birlik Günü",           HolidayType.Official, D(year,  7, 15), D(year,  7, 15)),
            new("Zafer Bayramı",                            HolidayType.Official, D(year,  8, 30), D(year,  8, 30)),
            new("Cumhuriyet Bayramı",                       HolidayType.Official, D(year, 10, 29), D(year, 10, 29)),
        };

        if (_islamicHolidays.TryGetValue(year, out var islamic))
            list.AddRange(islamic);

        AddHalfDay(year, list);
        return [.. list.OrderBy(h => h.StartDate)];
    }

    private static DateOnly D(int y, int m, int d) => new(y, m, d);

    // Known Islamic holiday dates for Turkey (Diyanet calendar, verified)
    private static readonly Dictionary<int, HolidayEntry[]> _islamicHolidays = new()
    {
        [2023] =
        [
            new("Ramazan Bayramı", HolidayType.Religious, D(2023,  4, 21), D(2023,  4, 23)),
            new("Kurban Bayramı",  HolidayType.Religious, D(2023,  6, 28), D(2023,  7,  1)),
        ],
        [2024] =
        [
            new("Ramazan Bayramı", HolidayType.Religious, D(2024,  4, 10), D(2024,  4, 12)),
            new("Kurban Bayramı",  HolidayType.Religious, D(2024,  6, 17), D(2024,  6, 20)),
        ],
        [2025] =
        [
            new("Ramazan Bayramı", HolidayType.Religious, D(2025,  3, 30), D(2025,  4,  1)),
            new("Kurban Bayramı",  HolidayType.Religious, D(2025,  6,  6), D(2025,  6,  9)),
        ],
        [2026] =
        [
            new("Ramazan Bayramı", HolidayType.Religious, D(2026,  3, 20), D(2026,  3, 22)),
            new("Kurban Bayramı",  HolidayType.Religious, D(2026,  5, 27), D(2026,  5, 30)),
        ],
        [2027] =
        [
            new("Ramazan Bayramı", HolidayType.Religious, D(2027,  3,  9), D(2027,  3, 11)),
            new("Kurban Bayramı",  HolidayType.Religious, D(2027,  5, 16), D(2027,  5, 19)),
        ],
    };

    // ─── DTO ────────────────────────────────────────────────────────────────────

    private sealed record NagerHolidayDto(
        [property: JsonPropertyName("date")]      DateTime Date,
        [property: JsonPropertyName("localName")] string   LocalName,
        [property: JsonPropertyName("name")]      string   Name,
        [property: JsonPropertyName("types")]     string[] Types);
}
