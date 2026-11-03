namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record AuthorizedWorkspaceSnapshot(Guid DepotId, Guid KeyId, IReadOnlyList<RoutingWorkspace> Workspaces);
