using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;

namespace Assist.SDLC.Services;

using Assist.SDLC.Abstractions;
using Assist.SDLC.Domain;

// ═══════════════════════════════════════════════════════════
// Notification Service
// ═══════════════════════════════════════════════════════════

internal sealed class NotificationService : INotificationService
{
    private readonly ConcurrentBag<NotificationItem> _items = [];

    public event EventHandler<NotificationItem>? NotificationReceived;

    public void Push(NotificationItem item)
    {
        _items.Add(item);
        NotificationReceived?.Invoke(this, item);
    }

    public IReadOnlyList<NotificationItem> GetAll() => [.. _items];
    public IReadOnlyList<NotificationItem> GetUnread() => [.. _items.Where(n => !n.IsRead)];

    public void MarkRead(string notificationId)
    {
        var item = _items.FirstOrDefault(n => n.Id == notificationId);
        if (item is not null) item.IsRead = true;
    }
}

// ═══════════════════════════════════════════════════════════
// Waiting Manager
// ═══════════════════════════════════════════════════════════

internal sealed class WaitingManagerService : IWaitingManager
{
    private readonly ConcurrentBag<WaitingEntry> _entries = [];

    public event EventHandler<WaitingEntry>? WaitingStarted;
    public event EventHandler<WaitingEntry>? WaitingResolved;

    public WaitingEntry Register(WaitingEntry entry)
    {
        _entries.Add(entry);
        WaitingStarted?.Invoke(this, entry);
        return entry;
    }

    public void Resolve(string waitingId)
    {
        var entry = _entries.FirstOrDefault(e => e.Id == waitingId);
        if (entry is null) return;
        entry.Resolved = true;
        entry.ResolvedUtc = DateTime.UtcNow;
        WaitingResolved?.Invoke(this, entry);
    }

    public IReadOnlyList<WaitingEntry> GetActive() =>
        [.. _entries.Where(e => !e.Resolved)];
}

// ═══════════════════════════════════════════════════════════
// Approval Service
// ═══════════════════════════════════════════════════════════

internal sealed class ApprovalService : IApprovalService
{
    private readonly ConcurrentBag<ApprovalRequest> _requests = [];

    public event EventHandler<ApprovalRequest>? ApprovalRequested;
    public event EventHandler<ApprovalRequest>? ApprovalDecided;

    public ApprovalRequest Request(ApprovalRequest request)
    {
        _requests.Add(request);
        ApprovalRequested?.Invoke(this, request);
        return request;
    }

    public void Decide(string approvalId, HumanAction decision, string? note = null)
    {
        var req = _requests.FirstOrDefault(r => r.Id == approvalId);
        if (req is null) return;
        req.Decision = decision;
        req.DecisionNote = note;
        req.DecidedUtc = DateTime.UtcNow;
        ApprovalDecided?.Invoke(this, req);
    }

    public IReadOnlyList<ApprovalRequest> GetPending() =>
        [.. _requests.Where(r => r.Decision is null)];

    public IReadOnlyList<ApprovalRequest> GetAll() => [.. _requests];
}

// ═══════════════════════════════════════════════════════════
// Human Decision Service
// ═══════════════════════════════════════════════════════════

internal sealed class HumanDecisionService : IHumanDecisionService
{
    private readonly ConcurrentBag<HumanOverrideRecord> _overrides = [];

    public void RecordOverride(HumanOverrideRecord record)
        => _overrides.Add(record);

    public IReadOnlyList<HumanOverrideRecord> GetOverrides(string? taskId = null) =>
        taskId is null
            ? [.. _overrides]
            : [.. _overrides.Where(o => o.TaskId == taskId)];
}

// ═══════════════════════════════════════════════════════════
// History Store  (JSON file-based, one file per category)
// ═══════════════════════════════════════════════════════════

internal sealed class HistoryStoreService : IHistoryStore
{
    private static readonly string _basePath = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "Assist", "SDLC", "History");

    public void Save<T>(string category, T record)
    {
        Directory.CreateDirectory(_basePath);
        var path = GetPath(category);
        var list = LoadInternal<T>(path);
        list.Add(record);
        File.WriteAllText(path, JsonSerializer.Serialize(list, _jsonOpts));
    }

    public IReadOnlyList<T> Load<T>(string category) =>
        LoadInternal<T>(GetPath(category));

    public IReadOnlyList<T> Load<T>(string category, Func<T, bool> filter) =>
        [.. LoadInternal<T>(GetPath(category)).Where(filter)];

    public void Clear(string category)
    {
        var path = GetPath(category);
        if (File.Exists(path)) File.Delete(path);
    }

    private static List<T> LoadInternal<T>(string path)
    {
        if (!File.Exists(path)) return [];
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<List<T>>(json, _jsonOpts) ?? [];
        }
        catch { return []; }
    }

    private static string GetPath(string category) =>
        Path.Combine(_basePath, $"{category}.json");

    private static readonly JsonSerializerOptions _jsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };
}

// ═══════════════════════════════════════════════════════════
// Documentation Service
// ═══════════════════════════════════════════════════════════

internal sealed class DocumentationService(IHistoryStore history) : IDocumentationService
{
    private const string Category = "documentation";

    public void AddSection(DocumentationSummary section)
        => history.Save(Category, section);

    public FinalOrchestrationReport GenerateReport(string taskId)
    {
        var sections = history.Load<DocumentationSummary>(Category, s => s.TaskId == taskId);
        var report = new FinalOrchestrationReport { TaskId = taskId };
        report.Sections.AddRange(sections);
        return report;
    }

    public EndOfDaySummary GenerateEndOfDaySummary()
    {
        // TODO: aggregate from history
        return new EndOfDaySummary();
    }

    public string GenerateConsolidatedMarkdown(string taskId)
    {
        var report = GenerateReport(taskId);
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# Orchestration Report — {taskId}");
        sb.AppendLine($"Generated: {report.GeneratedUtc:u}");
        sb.AppendLine();
        foreach (var s in report.Sections)
        {
            sb.AppendLine($"## {s.SectionTitle}");
            sb.AppendLine(s.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

// ═══════════════════════════════════════════════════════════
// IDE Session Service
// ═══════════════════════════════════════════════════════════

internal sealed class IdeSessionService : IIdeSessionService
{
    private readonly HashSet<int> _locked = [];

    public IReadOnlyList<IdeSessionInfo> DetectSessions()
    {
        var result = new List<IdeSessionInfo>();
        foreach (var name in new[] { "devenv", "Code" })
        {
            foreach (var proc in Process.GetProcessesByName(name))
            {
                try
                {
                    result.Add(new IdeSessionInfo
                    {
                        ProcessId = proc.Id,
                        ProcessName = proc.ProcessName,
                        WindowTitle = proc.MainWindowTitle,
                        IsLocked = _locked.Contains(proc.Id)
                    });
                }
                catch { /* access denied — skip */ }
                finally { proc.Dispose(); }
            }
        }
        return result;
    }

    public IdeSessionInfo? GetActive() =>
        DetectSessions().FirstOrDefault(s => !string.IsNullOrWhiteSpace(s.WindowTitle));

    public bool Lock(int processId)
    {
        lock (_locked) return _locked.Add(processId);
    }

    public void Unlock(int processId)
    {
        lock (_locked) _locked.Remove(processId);
    }
}

// ═══════════════════════════════════════════════════════════
// Report Service
// ═══════════════════════════════════════════════════════════

internal sealed class ReportService : IReportService
{
    public string ExportAsText(FinalOrchestrationReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"Task: {report.TaskId} | Generated: {report.GeneratedUtc:u}");
        foreach (var s in report.Sections)
            sb.AppendLine($"[{s.SectionTitle}] {s.Content}");
        return sb.ToString();
    }

    public string ExportAsMarkdown(FinalOrchestrationReport report)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"# {report.TaskId}");
        foreach (var s in report.Sections)
        {
            sb.AppendLine($"## {s.SectionTitle}");
            sb.AppendLine(s.Content);
            sb.AppendLine();
        }
        return sb.ToString();
    }

    public string ExportAsJson(FinalOrchestrationReport report) =>
        JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true });
}
