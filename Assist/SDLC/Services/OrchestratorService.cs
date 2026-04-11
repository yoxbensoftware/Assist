namespace Assist.SDLC.Services;

using Assist.SDLC.Abstractions;
using Assist.SDLC.Domain;
using Assist.SDLC.Messaging;

/// <summary>
/// Coordinates the full SDLC pipeline for a given task.
/// Skeleton — the actual step-chain logic will be filled later.
/// </summary>
internal sealed class OrchestratorService(
    IAgentCoordinator coordinator,
    IEventBus bus,
    INotificationService notifications) : IOrchestratorService
{
    public async Task RunAsync(SdlcTask task, CancellationToken ct = default)
    {
        bus.Publish(new SdlcEvent(SdlcEventType.TaskCreated, task.Id, Summary: task.Title));
        task.Status = SdlcTaskStatus.InProgress;

        // TODO: classification step
        bus.Publish(new SdlcEvent(SdlcEventType.TaskClassified, task.Id));

        // Pipeline skeleton — each step awaits the respective agent
        var pipeline = new AgentRole[]
        {
            AgentRole.ProductOwner,
            AgentRole.Analyst,
            AgentRole.Architect,
            AgentRole.Developer,
            AgentRole.Tester,
            AgentRole.Reviewer,
            AgentRole.Documentation
        };

        foreach (var role in pipeline)
        {
            ct.ThrowIfCancellationRequested();

            var agent = coordinator.GetAgent(role);
            await agent.ExecuteAsync(task, ct);

            if (agent.CurrentState == AgentState.Failed)
            {
                task.Status = SdlcTaskStatus.Failed;
                notifications.Push(new NotificationItem
                {
                    Severity = NotificationSeverity.Error,
                    Title = $"{role} failed",
                    Summary = $"Agent {role} failed for task {task.Id}",
                    SourceAgent = role,
                    TaskId = task.Id
                });
                return;
            }
        }

        task.Status = SdlcTaskStatus.Completed;
        task.CompletedUtc = DateTime.UtcNow;
        bus.Publish(new SdlcEvent(SdlcEventType.TaskCompleted, task.Id));
    }

    public void PauseAll()
    {
        foreach (var agent in coordinator.GetAllAgents())
            agent.Pause();
    }

    public void ResumeAll()
    {
        foreach (var agent in coordinator.GetAllAgents())
            agent.Resume();
    }

    public void CancelAll()
    {
        foreach (var agent in coordinator.GetAllAgents())
            agent.Cancel();
    }
}
