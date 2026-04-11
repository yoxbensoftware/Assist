namespace Assist.SDLC.Abstractions;

using Assist.SDLC.Domain;

// ═══════════════════════════════════════════════════════════
// Agent abstraction
// ═══════════════════════════════════════════════════════════

internal interface IAgent
{
    AgentRole Role { get; }
    AgentState CurrentState { get; }
    string? CurrentTaskId { get; }

    /// <summary>Runs the agent's primary workflow for the given task.</summary>
    Task ExecuteAsync(SdlcTask task, CancellationToken ct = default);

    void Pause();
    void Resume();
    void Cancel();

    AgentStateSnapshot GetSnapshot();

    /// <summary>Fires whenever the agent's state changes.</summary>
    event EventHandler<AgentState>? StateChanged;
}
