namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record AuthorizedWorkspaceSnapshot(Guid KeyId, IReadOnlyList<RoutingWorkspace> Workspaces,
    bool CanCreateRootWorkspaces = false);
