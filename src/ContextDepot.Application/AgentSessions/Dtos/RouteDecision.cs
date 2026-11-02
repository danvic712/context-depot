namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record RouteDecision(string Decision, Guid? WorkspaceId = null, string? ResultCode = null,
    string ResolverVersion = "workspace-router-v1", DateTimeOffset? NotBefore = null);
