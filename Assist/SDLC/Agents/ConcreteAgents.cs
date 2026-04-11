namespace Assist.SDLC.Agents;

using Assist.SDLC.Abstractions;
using Assist.SDLC.Domain;

// ═══════════════════════════════════════════════════════════
// Concrete agent stubs — override OnExecuteAsync to add
// business logic later.  Each agent is a thin skeleton now.
// ═══════════════════════════════════════════════════════════

internal sealed class ProductOwnerAgent(IEventBus bus) : AgentBase(AgentRole.ProductOwner, bus)
{
    protected override Task OnExecuteAsync(SdlcTask task, CancellationToken ct)
    {
        // TODO: user story, acceptance criteria, scope, priority analysis
        PublishEvent(SdlcEventType.PoDecisionReady, "PO decision placeholder");
        return Task.CompletedTask;
    }
}

internal sealed class AnalystAgent(IEventBus bus) : AgentBase(AgentRole.Analyst, bus)
{
    protected override Task OnExecuteAsync(SdlcTask task, CancellationToken ct)
    {
        // TODO: impact analysis, dependency analysis, risk, edge cases
        PublishEvent(SdlcEventType.AnalysisCompleted, "Analysis placeholder");
        return Task.CompletedTask;
    }
}

internal sealed class ArchitectAgent(IEventBus bus) : AgentBase(AgentRole.Architect, bus)
{
    protected override Task OnExecuteAsync(SdlcTask task, CancellationToken ct)
    {
        // TODO: pattern evaluation, architecture decision
        PublishEvent(SdlcEventType.ArchitectureDecisionReady, "Architecture placeholder");
        return Task.CompletedTask;
    }
}

internal sealed class DeveloperAgent(IEventBus bus) : AgentBase(AgentRole.Developer, bus)
{
    protected override Task OnExecuteAsync(SdlcTask task, CancellationToken ct)
    {
        // TODO: implementation plan, refactor plan, IDE prompt generation
        PublishEvent(SdlcEventType.DeveloperPlanReady, "Developer plan placeholder");
        return Task.CompletedTask;
    }
}

internal sealed class TesterAgent(IEventBus bus) : AgentBase(AgentRole.Tester, bus)
{
    protected override Task OnExecuteAsync(SdlcTask task, CancellationToken ct)
    {
        // TODO: unit/integration/performance test plans
        PublishEvent(SdlcEventType.TestsPassed, "Test plan placeholder");
        return Task.CompletedTask;
    }
}

internal sealed class ReviewerAgent(IEventBus bus) : AgentBase(AgentRole.Reviewer, bus)
{
    protected override Task OnExecuteAsync(SdlcTask task, CancellationToken ct)
    {
        // TODO: code review, quality gate, security check
        PublishEvent(SdlcEventType.ReviewCompleted, "Review placeholder");
        return Task.CompletedTask;
    }
}

internal sealed class DocumentationAgent(IEventBus bus) : AgentBase(AgentRole.Documentation, bus)
{
    protected override Task OnExecuteAsync(SdlcTask task, CancellationToken ct)
    {
        // TODO: generate delta-based documentation summaries
        PublishEvent(SdlcEventType.DocumentationUpdated, "Documentation placeholder");
        return Task.CompletedTask;
    }
}
