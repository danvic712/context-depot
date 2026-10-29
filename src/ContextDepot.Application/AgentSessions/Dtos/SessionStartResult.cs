namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionStartResult(string Status, SessionModel? Session);
