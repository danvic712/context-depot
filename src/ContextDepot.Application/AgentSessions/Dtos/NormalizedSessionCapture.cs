namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record NormalizedSessionCapture(string ClientSessionId, string TurnId,
    IReadOnlyList<SessionCaptureMessage> Messages, IReadOnlyList<string> EventIds);
