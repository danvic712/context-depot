using ContextDepot.Infrastructure.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Infrastructure.Access;

public sealed class WorkspaceAccessContext : IWorkspaceAccessContext
{
    private Guid? accessKeyId;
    private Guid[] workspaceIds = [];
    private Guid[] navigableWorkspaceIds = [];
    private bool initialized;

    public Guid? AccessKeyId => accessKeyId;

    public bool HasUnrestrictedAccess { get; private set; }

    public bool CanCreateRootWorkspaces { get; private set; }

    public long AccessVersion { get; private set; }

    public IReadOnlyList<Guid> WorkspaceIds => workspaceIds;

    public IReadOnlyList<Guid> NavigableWorkspaceIds => navigableWorkspaceIds;

    public bool CanAccess(Guid workspaceId) =>
        HasUnrestrictedAccess || Array.BinarySearch(workspaceIds, workspaceId) >= 0;

    public void Initialize(AccessKeyIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (initialized)
        {
            throw new InvalidOperationException(InfrastructureErrorCodes.WorkspaceAccessContextAlreadyInitialized);
        }

        accessKeyId = identity.AccessKeyId;
        workspaceIds = identity.WorkspaceIds.Distinct().Order().ToArray();
        navigableWorkspaceIds = identity.NavigableWorkspaceIds.Distinct().Order().ToArray();
        HasUnrestrictedAccess = false;
        CanCreateRootWorkspaces = identity.CanCreateRootWorkspaces;
        AccessVersion = identity.AccessVersion;
        initialized = true;
    }

    public void UpdateWorkspaceAccess(IReadOnlyCollection<Guid> accessibleIds, IReadOnlyCollection<Guid> navigableIds,
        bool canCreateRootWorkspaces, long accessVersion)
    {
        if (HasUnrestrictedAccess) return;
        AccessVersion = accessVersion;
        CanCreateRootWorkspaces = canCreateRootWorkspaces;
        workspaceIds = accessibleIds.Distinct().Order().ToArray();
        navigableWorkspaceIds = navigableIds.Distinct().Order().ToArray();
    }

    public void AllowInternalAccess()
    {
        if (initialized)
        {
            throw new InvalidOperationException(InfrastructureErrorCodes.WorkspaceAccessContextAlreadyInitialized);
        }

        HasUnrestrictedAccess = true;
        CanCreateRootWorkspaces = true;
        initialized = true;
    }
}
