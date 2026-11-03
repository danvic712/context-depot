namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record RoutingWorkspace(Guid Id, string Name, string Path, string? Description);
