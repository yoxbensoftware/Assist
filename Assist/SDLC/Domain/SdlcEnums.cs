namespace Assist.SDLC.Domain;

// ── Agent ─────────────────────────────────────────────────

internal enum AgentRole
{
    ProductOwner,
    Analyst,
    Architect,
    Developer,
    Tester,
    Reviewer,
    Documentation
}

internal enum AgentState
{
    Idle,
    Listening,
    Waiting,
    Running,
    Reviewing,
    NeedsApproval,
    Blocked,
    Failed,
    Completed,
    Escalated,
    Paused,
    Cancelled
}

// ── Events ────────────────────────────────────────────────

internal enum SdlcEventType
{
    TaskCreated,
    TaskClassified,
    PoDecisionReady,
    AnalysisCompleted,
    ArchitectureDecisionReady,
    DeveloperPlanReady,
    ConsoleCommandStarted,
    ConsoleCommandCompleted,
    BuildSucceeded,
    BuildFailed,
    TestsPassed,
    TestsFailed,
    ReviewRequested,
    ReviewCompleted,
    DocumentationUpdated,
    ApprovalRequested,
    ApprovalGranted,
    ApprovalRejected,
    TimeoutOccurred,
    WaitingStarted,
    WaitingEnded,
    EscalationRaised,
    HumanOverrideApplied,
    TaskCompleted,
    TaskCancelled,
    AgentStateChanged
}

// ── Task ──────────────────────────────────────────────────

internal enum SdlcTaskPriority { Low, Medium, High, Critical }

internal enum SdlcTaskRiskLevel { Low, Medium, High, Critical }

internal enum SdlcTaskStatus
{
    Draft,
    Submitted,
    Classifying,
    InProgress,
    WaitingApproval,
    Completed,
    Failed,
    Cancelled
}

// ── Infrastructure ────────────────────────────────────────

internal enum WaitingReason
{
    HumanApproval,
    AnotherAgent,
    BuildResult,
    TestResult,
    IdeAvailability,
    ResourceLock,
    RetryWindow,
    ScheduledExecution
}

internal enum NotificationSeverity
{
    Info,
    Success,
    Warning,
    Error,
    ApprovalRequest,
    Escalation,
    TimeoutAlert
}

internal enum HumanAction
{
    Approve,
    Reject,
    Retry,
    ForceContinue,
    ForceStop,
    Pause,
    Resume,
    Redirect,
    Override,
    Skip,
    ChangePriority,
    RequestExtraReview,
    MarkCompleted,
    AddNote
}
