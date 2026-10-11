namespace ContextDepot.Application.Shared.Runtime.Contracts;

public interface IWorkspaceAccessContext
{
    Guid? DepotAccessKeyId { get; }

    bool HasUnrestrictedAccess { get; }

    bool CanCreateRootWorkspaces { get; }

    long AccessVersion { get; }

    IReadOnlyList<Guid> WorkspaceIds { get; }

    IReadOnlyList<Guid> NavigableWorkspaceIds { get; }

    bool CanAccess(Guid workspaceId);

    void UpdateWorkspaceAccess(IReadOnlyCollection<Guid> workspaceIds, IReadOnlyCollection<Guid> navigableIds,
        bool canCreateRootWorkspaces, long accessVersion);
}
