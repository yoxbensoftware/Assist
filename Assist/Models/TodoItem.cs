namespace Assist.Models;

internal enum TodoPriority   { Low = 0, Normal = 1, High = 2, Critical = 3 }
internal enum RecurrenceType { None, Monthly }

/// <summary>
/// Represents a single to-do task with deadline and priority tracking.
/// </summary>
internal sealed class TodoItem
{
    public Guid           Id             { get; set; } = Guid.NewGuid();
    public string         Title          { get; set; } = string.Empty;
    public string         Description    { get; set; } = string.Empty;
    public string         Category       { get; set; } = string.Empty;
    public TodoPriority   Priority       { get; set; } = TodoPriority.Normal;
    public DateTime?      DueDate        { get; set; }
    public bool           IsCompleted    { get; set; }
    public DateTime       CreatedAt      { get; set; } = DateTime.Now;
    public DateTime?      CompletedAt    { get; set; }

    // ── Periyodik tekrar ──────────────────────────────────────────────────
    public bool           IsRecurring    { get; set; }
    public RecurrenceType RecurrenceType { get; set; }
    public int            RecurrenceDay  { get; set; } = 1;  // ayın kaçında (1-28)

    [System.Text.Json.Serialization.JsonIgnore]
    public string RecurrenceLabel => (IsRecurring, RecurrenceType) switch
    {
        (true, RecurrenceType.Monthly) when RecurrenceDay == 1 => "Her ay 1.",
        (true, RecurrenceType.Monthly)                         => $"Her ay {RecurrenceDay}.",
        _                                                      => ""
    };

    /// <summary>Verilen tarihten sonraki bir sonraki tekrar tarihini döner.</summary>
    public DateTime NextOccurrenceAfter(DateTime from)
    {
        if (RecurrenceType == RecurrenceType.Monthly)
        {
            var first = new DateTime(from.Year, from.Month, 1).AddMonths(1);
            int day   = Math.Min(RecurrenceDay, DateTime.DaysInMonth(first.Year, first.Month));
            return new DateTime(first.Year, first.Month, day);
        }
        return from.AddDays(30);
    }

    // ── Hesaplanan özellikler ─────────────────────────────────────────────
    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsOverdue =>
        !IsCompleted && DueDate.HasValue && DueDate.Value.Date < DateTime.Today;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDueToday =>
        !IsCompleted && DueDate.HasValue && DueDate.Value.Date == DateTime.Today;

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDueTomorrow =>
        !IsCompleted && DueDate.HasValue && DueDate.Value.Date == DateTime.Today.AddDays(1);

    [System.Text.Json.Serialization.JsonIgnore]
    public bool IsDueThisWeek =>
        !IsCompleted && DueDate.HasValue
        && DueDate.Value.Date > DateTime.Today.AddDays(1)
        && DueDate.Value.Date <= DateTime.Today.AddDays(7);

    [System.Text.Json.Serialization.JsonIgnore]
    public string TimeLeftText
    {
        get
        {
            if (IsCompleted)       return "✅ Tamamlandı";
            if (!DueDate.HasValue) return IsRecurring ? RecurrenceLabel : "—";
            var diff = DueDate.Value.Date - DateTime.Today;
            if (diff.TotalDays <  0) return $"⚠ {Math.Abs((int)diff.TotalDays)} gün gecikmiş";
            if (diff.TotalDays == 0) return "🔥 Bugün!";
            if (diff.TotalDays == 1) return "⏰ Yarın";
            return $"📅 {(int)diff.TotalDays} gün kaldı";
        }
    }

    [System.Text.Json.Serialization.JsonIgnore]
    public string PriorityText => Priority switch
    {
        TodoPriority.Critical => "🔴 Kritik",
        TodoPriority.High     => "🟠 Yüksek",
        TodoPriority.Normal   => "🟡 Normal",
        TodoPriority.Low      => "⚪ Düşük",
        _                     => ""
    };
}
