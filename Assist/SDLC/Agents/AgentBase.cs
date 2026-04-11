namespace Assist.SDLC.Agents;

using Assist.SDLC.Abstractions;
using Assist.SDLC.Domain;
using Assist.SDLC.Messaging;

/// <summary>
/// Base class for all SDLC agents. Provides state machine, event publishing,
/// and history tracking. Subclasses override <see cref="OnExecuteAsync"/>.
/// </summary>
internal abstract class AgentBase : IAgent
{
    private readonly IEventBus _bus;
    private AgentState _state = AgentState.Idle;
    private CancellationTokenSource? _cts;

    protected AgentBase(AgentRole role, IEventBus bus)
    {
        Role = role;
        _bus = bus;
    }

    // ── IAgent ────────────────────────────────────────────

    public AgentRole Role { get; }

    public AgentState CurrentState
    {
        get => _state;
        private set
        {
            if (_state == value) return;
            _state = value;
            StateChanged?.Invoke(this, value);
            _bus.Publish(new SdlcEvent(
                SdlcEventType.AgentStateChanged,
                CurrentTaskId,
                Role,
                $"{Role} → {value}"));
        }
    }

    public string? CurrentTaskId { get; private set; }
    public event EventHandler<AgentState>? StateChanged;

    public async Task ExecuteAsync(SdlcTask task, CancellationToken ct = default)
    {
        _cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        CurrentTaskId = task.Id;
        CurrentState = AgentState.Running;

        try
        {
            await OnExecuteAsync(task, _cts.Token);
            CurrentState = AgentState.Completed;
        }
        catch (OperationCanceledException)
        {
            CurrentState = AgentState.Cancelled;
        }
        catch
        {
            CurrentState = AgentState.Failed;
        }
        finally
        {
            _cts.Dispose();
            _cts = null;
        }
    }

    public void Pause() => CurrentState = AgentState.Paused;
    public void Resume() => CurrentState = AgentState.Running;
    public void Cancel()
    {
        _cts?.Cancel();
        CurrentState = AgentState.Cancelled;
    }

    public AgentStateSnapshot GetSnapshot() =>
        new(Role, CurrentState, CurrentTaskId, DateTime.UtcNow);

    // ── Extension point ───────────────────────────────────

    /// <summary>
    /// Override in each concrete agent to perform the agent's actual work.
    /// </summary>
    protected abstract Task OnExecuteAsync(SdlcTask task, CancellationToken ct);

    // ── Helpers for subclasses ────────────────────────────

    protected void PublishEvent(SdlcEventType type, string? summary = null, object? payload = null)
        => _bus.Publish(new SdlcEvent(type, CurrentTaskId, Role, summary, payload));

    protected void TransitionTo(AgentState state) => CurrentState = state;
}
