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

    public IReadOnlyList<Guid> WorkspaceIds => workspaceIds;

    public IReadOnlyList<Guid> NavigableWorkspaceIds => navigableWorkspaceIds;

    public bool CanAccess(Guid workspaceId) =>
        HasUnrestrictedAccess || Array.BinarySearch(workspaceIds, workspaceId) >= 0;

    public void Initialize(DepotAccessKeyIdentity identity)
    {
        ArgumentNullException.ThrowIfNull(identity);
        if (initialized)
        {
            throw new InvalidOperationException("The current depot access context has already been initialized.");
        }

        depotAccessKeyId = identity.DepotAccessKeyId;
        depotId = identity.DepotId;
        displayName = identity.DepotDisplayName;
        workspaceIds = identity.WorkspaceIds.Distinct().Order().ToArray();
        navigableWorkspaceIds = identity.NavigableWorkspaceIds.Distinct().Order().ToArray();
        HasUnrestrictedAccess = false;
        initialized = true;
    }

    public void AllowInternalAccess()
    {
        if (initialized)
        {
            throw new InvalidOperationException("The current depot access context has already been initialized.");
        }

        HasUnrestrictedAccess = true;
        initialized = true;
    }
}
