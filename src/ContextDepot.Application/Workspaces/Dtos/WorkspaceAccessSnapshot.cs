namespace ContextDepot.Application.Workspaces.Dtos;

// Same-depot internal snapshots are never returned as public resolution candidates.
public sealed record WorkspaceAccessSnapshot(Guid? KeyId, bool Unrestricted, bool IsActive,
    bool CanCreateRootWorkspaces, long AccessVersion, IReadOnlyList<Guid> WorkspaceIds)
{
    public bool CanAccess(Guid id) => IsActive && (Unrestricted || WorkspaceIds.Contains(id));
}
