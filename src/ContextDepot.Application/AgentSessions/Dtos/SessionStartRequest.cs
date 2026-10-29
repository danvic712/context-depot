namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record SessionStartRequest(string Agent, string ClientSessionId, string Workspace);
