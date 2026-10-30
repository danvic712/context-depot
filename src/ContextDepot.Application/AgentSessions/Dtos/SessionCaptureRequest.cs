namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionCaptureRequest(int SchemaVersion, string Agent, string? ClientSessionId,
    string? TurnId, string? Phase, string EventId, string Workspace, string? Content,
    string? Mode = null, IReadOnlyList<SessionCaptureMessage>? Messages = null, Guid? SessionId = null);
