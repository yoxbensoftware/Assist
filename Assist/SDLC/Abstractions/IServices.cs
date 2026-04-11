namespace Assist.SDLC.Abstractions;

using Assist.SDLC.Domain;
using Assist.SDLC.Messaging;

// ═══════════════════════════════════════════════════════════
// Orchestration
// ═══════════════════════════════════════════════════════════

internal interface IOrchestratorService
{
    Task RunAsync(SdlcTask task, CancellationToken ct = default);
    void PauseAll();
    void ResumeAll();
    void CancelAll();
}

internal interface IAgentCoordinator
{
    IAgent GetAgent(AgentRole role);
    IReadOnlyList<IAgent> GetAllAgents();
    AgentStateSnapshot[] GetAllSnapshots();
}

internal interface ITaskClassifier
{
    TaskClassificationResult Classify(SdlcTask task);
}

// ═══════════════════════════════════════════════════════════
// Messaging
// ═══════════════════════════════════════════════════════════

internal interface IEventBus
{
    void Publish(SdlcEvent evt);
    IDisposable Subscribe(SdlcEventType type, Action<SdlcEvent> handler);
    IDisposable SubscribeAll(Action<SdlcEvent> handler);
    IReadOnlyList<SdlcEvent> GetHistory();
}

// ═══════════════════════════════════════════════════════════
// Notifications / Waiting / Approval
// ═══════════════════════════════════════════════════════════

internal interface INotificationService
{
    void Push(NotificationItem item);
    IReadOnlyList<NotificationItem> GetAll();
    IReadOnlyList<NotificationItem> GetUnread();
    void MarkRead(string notificationId);
    event EventHandler<NotificationItem>? NotificationReceived;
}

internal interface IWaitingManager
{
    WaitingEntry Register(WaitingEntry entry);
    void Resolve(string waitingId);
    IReadOnlyList<WaitingEntry> GetActive();
    event EventHandler<WaitingEntry>? WaitingStarted;
    event EventHandler<WaitingEntry>? WaitingResolved;
}

internal interface IApprovalService
{
    ApprovalRequest Request(ApprovalRequest request);
    void Decide(string approvalId, HumanAction decision, string? note = null);
    IReadOnlyList<ApprovalRequest> GetPending();
    IReadOnlyList<ApprovalRequest> GetAll();
    event EventHandler<ApprovalRequest>? ApprovalRequested;
    event EventHandler<ApprovalRequest>? ApprovalDecided;
}

internal interface IHumanDecisionService
{
    void RecordOverride(HumanOverrideRecord record);
    IReadOnlyList<HumanOverrideRecord> GetOverrides(string? taskId = null);
}

// ═══════════════════════════════════════════════════════════
// Execution
// ═══════════════════════════════════════════════════════════

internal interface IConsoleCommandService
{
    Task<ConsoleCommandResult> RunAsync(ConsoleCommandRequest request, CancellationToken ct = default);
    event EventHandler<string>? OutputReceived;
}

internal interface IBuildService
{
    Task<BuildValidationResult> BuildAsync(string solutionOrProjectPath, CancellationToken ct = default);
}

internal interface ITestService
{
    Task<TestValidationResult> RunTestsAsync(string solutionOrProjectPath, CancellationToken ct = default);
}

// ═══════════════════════════════════════════════════════════
// IDE
// ═══════════════════════════════════════════════════════════

internal interface IIdeSessionService
{
    IReadOnlyList<IdeSessionInfo> DetectSessions();
    IdeSessionInfo? GetActive();
    bool Lock(int processId);
    void Unlock(int processId);
}

// ═══════════════════════════════════════════════════════════
// Persistence / History
// ═══════════════════════════════════════════════════════════

internal interface IHistoryStore
{
    void Save<T>(string category, T record);
    IReadOnlyList<T> Load<T>(string category);
    IReadOnlyList<T> Load<T>(string category, Func<T, bool> filter);
    void Clear(string category);
}

// ═══════════════════════════════════════════════════════════
// Documentation
// ═══════════════════════════════════════════════════════════

internal interface IDocumentationService
{
    void AddSection(DocumentationSummary section);
    FinalOrchestrationReport GenerateReport(string taskId);
    EndOfDaySummary GenerateEndOfDaySummary();
    string GenerateConsolidatedMarkdown(string taskId);
}

// ═══════════════════════════════════════════════════════════
// Reporting
// ═══════════════════════════════════════════════════════════

internal interface IReportService
{
    string ExportAsText(FinalOrchestrationReport report);
    string ExportAsMarkdown(FinalOrchestrationReport report);
    string ExportAsJson(FinalOrchestrationReport report);
}

// ═══════════════════════════════════════════════════════════
// Health
// ═══════════════════════════════════════════════════════════

internal interface IHealthMonitorService
{
    bool IsHealthy { get; }
    IReadOnlyDictionary<AgentRole, AgentState> GetAgentHealth();
}
