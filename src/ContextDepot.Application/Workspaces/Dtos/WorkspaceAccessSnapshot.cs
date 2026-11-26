namespace ContextDepot.Application.Workspaces.Dtos;

// Internal snapshots are for workspace writes.
public sealed record WorkspaceAccessSnapshot(Guid? KeyId, bool Unrestricted, bool IsActive,
    bool CanCreateRootWorkspaces, long AccessVersion, IReadOnlyList<Guid> WorkspaceIds)
{
    public bool CanAccess(Guid id) => IsActive && (Unrestricted || WorkspaceIds.Contains(id));
}
