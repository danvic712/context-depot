using ContextDepot.Domain.AgentSessions;

namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record AuthorizedCaptureEnvelope(IReadOnlyList<SessionCaptureInboxEvent> Messages, bool Sensitive, DateTimeOffset ExpiresAt);
