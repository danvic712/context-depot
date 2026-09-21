namespace ContextDepot.Application.Shared.Runtime.Contracts;

public interface IWorkspaceAccessContext
{
    Guid? DepotAccessKeyId { get; }

    bool HasUnrestrictedAccess { get; }

    IReadOnlyList<Guid> WorkspaceIds { get; }

    IReadOnlyList<Guid> NavigableWorkspaceIds { get; }

    bool CanAccess(Guid workspaceId);
}
