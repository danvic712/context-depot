using ContextDepot.Infrastructure.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Infrastructure.CurrentDepot;

public sealed class CurrentDepotAccessContext : ICurrentDepotContext, IWorkspaceAccessContext
{
    private Guid? depotAccessKeyId;
    private Guid depotId;
    private string displayName = string.Empty;
    private Guid[] workspaceIds = [];
    private Guid[] navigableWorkspaceIds = [];
    private bool initialized;

    public Guid DepotId => depotId;

    public string DisplayName => displayName;

    public Guid? DepotAccessKeyId => depotAccessKeyId;

    public bool HasUnrestrictedAccess { get; private set; }

    public bool CanCreateRootWorkspaces { get; private set; }

    public long AccessVersion { get; private set; }

    public IReadOnlyList<Guid> WorkspaceIds => workspaceIds;

    public IReadOnlyList<Guid> NavigableWorkspaceIds => navigableWorkspaceIds;

    public bool CanAccess(Guid workspaceId) =>
        HasUnrestrictedAccess || Array.BinarySearch(workspaceIds, workspaceId) >= 0;

    public void Initialize(DepotAccessKeyIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (initialized)
        {
            throw new InvalidOperationException(InfrastructureErrorCodes.DepotAccessContextAlreadyInitialized);
        }

        depotAccessKeyId = identity.DepotAccessKeyId;
        depotId = identity.DepotId;
        displayName = identity.DepotDisplayName;
        workspaceIds = identity.WorkspaceIds.Distinct().Order().ToArray();
        navigableWorkspaceIds = identity.NavigableWorkspaceIds.Distinct().Order().ToArray();
        HasUnrestrictedAccess = false;
        CanCreateRootWorkspaces = identity.CanCreateRootWorkspaces;
        AccessVersion = identity.AccessVersion;
        initialized = true;
    }

    public void InitializeForWeb(Guid id, string name)
    {
        if (initialized) throw new InvalidOperationException(InfrastructureErrorCodes.DepotAccessContextAlreadyInitialized);
        if (id == Guid.Empty) throw new ArgumentException(InfrastructureErrorCodes.WebDepotRequired, nameof(id));
        depotId = id;
        displayName = name;
        HasUnrestrictedAccess = true;
        CanCreateRootWorkspaces = true;
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
            throw new InvalidOperationException(InfrastructureErrorCodes.DepotAccessContextAlreadyInitialized);
        }

        HasUnrestrictedAccess = true;
        CanCreateRootWorkspaces = true;
        initialized = true;
    }
}
