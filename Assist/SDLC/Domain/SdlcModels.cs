using System.Collections.Concurrent;

namespace Assist.SDLC.Domain;

// ── Task ──────────────────────────────────────────────────

internal sealed class SdlcTask
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..12];
    public string Title { get; set; } = string.Empty;
    public string Prompt { get; set; } = string.Empty;
    public string? SolutionPath { get; set; }
    public string? ProjectPath { get; set; }
    public SdlcTaskPriority Priority { get; set; } = SdlcTaskPriority.Medium;
    public SdlcTaskRiskLevel RiskLevel { get; set; } = SdlcTaskRiskLevel.Medium;
    public SdlcTaskStatus Status { get; set; } = SdlcTaskStatus.Draft;
    public bool RequiresHumanApproval { get; set; }
    public string? Notes { get; set; }
    public DateTime CreatedUtc { get; init; } = DateTime.UtcNow;
    public DateTime? CompletedUtc { get; set; }
}

// ── Classification ────────────────────────────────────────

internal sealed record TaskClassificationResult(
    string TaskId,
    string Category,
    AgentRole[] RequiredAgents,
    string Rationale);

// ── Agent snapshots ───────────────────────────────────────

internal sealed record AgentStateSnapshot(
    AgentRole Role,
    AgentState State,
    string? CurrentTaskId,
    DateTime TimestampUtc);

internal sealed record AgentActionRecord(
    AgentRole Role,
    string TaskId,
    string Action,
    string? Summary,
    DateTime TimestampUtc);

internal sealed record AgentDecisionRecord(
    AgentRole Role,
    string TaskId,
    string Decision,
    string? Rationale,
    DateTime TimestampUtc);

// ── Console execution ─────────────────────────────────────

internal sealed class ConsoleCommandRequest
{
    public string Command { get; init; } = string.Empty;
    public string? WorkingDirectory { get; set; }
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(5);
    public int MaxRetries { get; set; }
    public string? TaskId { get; set; }
}

internal sealed class ConsoleCommandResult
{
    public string Command { get; init; } = string.Empty;
    public int ExitCode { get; set; }
    public string Stdout { get; set; } = string.Empty;
    public string Stderr { get; set; } = string.Empty;
    public TimeSpan Duration { get; set; }
    public bool TimedOut { get; set; }
    public bool Cancelled { get; set; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

// ── Build / Test ──────────────────────────────────────────

internal sealed record BuildValidationResult(
    bool Success,
    int ErrorCount,
    int WarningCount,
    string[] Errors,
    string[] Warnings,
    DateTime TimestampUtc);

internal sealed record TestValidationResult(
    bool AllPassed,
    int Total,
    int Passed,
    int Failed,
    int Skipped,
    string[] FailedTests,
    DateTime TimestampUtc);

// ── Review ────────────────────────────────────────────────

internal sealed record ReviewResult(
    string TaskId,
    bool Approved,
    string Summary,
    string[] Issues,
    string? RollbackNote,
    DateTime TimestampUtc);

// ── Documentation ─────────────────────────────────────────

internal sealed record DocumentationSummary(
    string TaskId,
    string SectionTitle,
    string Content,
    DateTime TimestampUtc);

// ── Notification ──────────────────────────────────────────

internal sealed class NotificationItem
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..10];
    public NotificationSeverity Severity { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Summary { get; init; } = string.Empty;
    public AgentRole? SourceAgent { get; init; }
    public string? TaskId { get; init; }
    public bool IsRead { get; set; }
    public DateTime TimestampUtc { get; init; } = DateTime.UtcNow;
}

// ── Waiting ───────────────────────────────────────────────

internal sealed class WaitingEntry
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..10];
    public string? TaskId { get; init; }
    public AgentRole? WaitingAgent { get; init; }
    public WaitingReason Reason { get; init; }
    public DateTime StartedUtc { get; init; } = DateTime.UtcNow;
    public TimeSpan MaxWait { get; set; } = TimeSpan.FromMinutes(30);
    public TimeSpan EscalationThreshold { get; set; } = TimeSpan.FromMinutes(20);
    public bool Resolved { get; set; }
    public DateTime? ResolvedUtc { get; set; }
}

// ── Approval ──────────────────────────────────────────────

internal sealed class ApprovalRequest
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N")[..10];
    public string? TaskId { get; init; }
    public AgentRole RequestingAgent { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Details { get; init; } = string.Empty;
    public HumanAction? Decision { get; set; }
    public string? DecisionNote { get; set; }
    public DateTime RequestedUtc { get; init; } = DateTime.UtcNow;
    public DateTime? DecidedUtc { get; set; }
}

// ── Human override ────────────────────────────────────────

internal sealed record HumanOverrideRecord(
    string TaskId,
    HumanAction Action,
    string? Note,
    AgentRole? TargetAgent,
    DateTime TimestampUtc);

// ── Timeline ──────────────────────────────────────────────

internal sealed record TimelineEntry(
    string TaskId,
    SdlcEventType EventType,
    AgentRole? Agent,
    string Summary,
    DateTime TimestampUtc);

// ── IDE Session ───────────────────────────────────────────

internal sealed class IdeSessionInfo
{
    public int ProcessId { get; init; }
    public string ProcessName { get; init; } = string.Empty;
    public string? SolutionPath { get; set; }
    public string? WindowTitle { get; set; }
    public bool IsLocked { get; set; }
    public DateTime DetectedUtc { get; init; } = DateTime.UtcNow;
}

// ── Policies ──────────────────────────────────────────────

internal sealed record RetryPolicy(int MaxRetries, TimeSpan Delay);

internal sealed record TimeoutPolicy(TimeSpan Duration, bool Escalate);

internal sealed record WaitingPolicy(TimeSpan MaxWait, TimeSpan EscalationThreshold, bool AutoRemind);

// ── Report ────────────────────────────────────────────────

internal sealed class FinalOrchestrationReport
{
    public string TaskId { get; init; } = string.Empty;
    public List<DocumentationSummary> Sections { get; } = [];
    public DateTime GeneratedUtc { get; init; } = DateTime.UtcNow;
}

internal sealed class EndOfDaySummary
{
    public DateTime Date { get; init; } = DateTime.Today;
    public int TasksCompleted { get; set; }
    public int TasksFailed { get; set; }
    public int ApprovalsGiven { get; set; }
    public List<string> Highlights { get; } = [];
}
