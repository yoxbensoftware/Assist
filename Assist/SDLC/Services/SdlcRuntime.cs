namespace Assist.SDLC.Services;

using Assist.SDLC.Abstractions;
using Assist.SDLC.Agents;
using Assist.SDLC.Domain;
using Assist.SDLC.Messaging;

/// <summary>
/// Static composition root for the SDLC module.
/// Call <see cref="EnsureInitialized"/> once before using any service.
/// All singletons are lazily created on first use.
/// </summary>
internal static class SdlcRuntime
{
    private static readonly object _lock = new();
    private static bool _initialized;

    // ── Singletons ────────────────────────────────────────

    public static IEventBus EventBus { get; private set; } = null!;
    public static IAgentCoordinator AgentCoordinator { get; private set; } = null!;
    public static IOrchestratorService Orchestrator { get; private set; } = null!;
    public static INotificationService Notifications { get; private set; } = null!;
    public static IWaitingManager WaitingManager { get; private set; } = null!;
    public static IApprovalService Approvals { get; private set; } = null!;
    public static IHumanDecisionService HumanDecisions { get; private set; } = null!;
    public static IConsoleCommandService ConsoleCommand { get; private set; } = null!;
    public static IHistoryStore History { get; private set; } = null!;
    public static IDocumentationService Documentation { get; private set; } = null!;
    public static IIdeSessionService IdeSessions { get; private set; } = null!;
    public static IReportService Reports { get; private set; } = null!;

    // ── Initialization ────────────────────────────────────

    public static void EnsureInitialized()
    {
        if (_initialized) return;
        lock (_lock)
        {
            if (_initialized) return;

            var bus = new EventBus();
            EventBus = bus;

            var coordinator = new AgentCoordinator(bus);
            AgentCoordinator = coordinator;

            Notifications = new NotificationService();
            WaitingManager = new WaitingManagerService();
            Approvals = new ApprovalService();
            HumanDecisions = new HumanDecisionService();
            ConsoleCommand = new ConsoleCommandService();
            History = new HistoryStoreService();
            Documentation = new DocumentationService(History);
            IdeSessions = new IdeSessionService();
            Reports = new ReportService();

            Orchestrator = new OrchestratorService(coordinator, bus, Notifications);

            _initialized = true;
        }
    }
}
