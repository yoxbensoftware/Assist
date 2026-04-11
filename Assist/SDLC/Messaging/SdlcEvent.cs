namespace Assist.SDLC.Messaging;

using Assist.SDLC.Domain;

/// <summary>
/// Immutable event exchanged between agents and services via the event bus.
/// </summary>
internal sealed record SdlcEvent(
    SdlcEventType Type,
    string? TaskId = null,
    AgentRole? SourceAgent = null,
    string? Summary = null,
    object? Payload = null)
{
    public string Id { get; } = Guid.NewGuid().ToString("N")[..10];
    public DateTime TimestampUtc { get; } = DateTime.UtcNow;
}
