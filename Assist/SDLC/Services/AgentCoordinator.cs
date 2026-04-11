namespace Assist.SDLC.Services;

using Assist.SDLC.Abstractions;
using Assist.SDLC.Agents;
using Assist.SDLC.Domain;

/// <summary>
/// Creates and holds all agent instances; routes requests to the right agent.
/// </summary>
internal sealed class AgentCoordinator : IAgentCoordinator
{
    private readonly Dictionary<AgentRole, IAgent> _agents;

    public AgentCoordinator(IEventBus bus)
    {
        _agents = new Dictionary<AgentRole, IAgent>
        {
            [AgentRole.ProductOwner]  = new ProductOwnerAgent(bus),
            [AgentRole.Analyst]       = new AnalystAgent(bus),
            [AgentRole.Architect]     = new ArchitectAgent(bus),
            [AgentRole.Developer]     = new DeveloperAgent(bus),
            [AgentRole.Tester]        = new TesterAgent(bus),
            [AgentRole.Reviewer]      = new ReviewerAgent(bus),
            [AgentRole.Documentation] = new DocumentationAgent(bus),
        };
    }

    public IAgent GetAgent(AgentRole role) =>
        _agents.TryGetValue(role, out var agent)
            ? agent
            : throw new ArgumentException($"Unknown agent role: {role}");

    public IReadOnlyList<IAgent> GetAllAgents() => [.. _agents.Values];

    public AgentStateSnapshot[] GetAllSnapshots() =>
        [.. _agents.Values.Select(a => a.GetSnapshot())];
}
