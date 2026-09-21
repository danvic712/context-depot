using ContextDepot.Domain.Depots;

namespace ContextDepot.Domain.Workspaces;

public sealed class WorkspaceAccessGrant
{
    private WorkspaceAccessGrant()
    {
    }

    public WorkspaceAccessGrant(
        Guid depotAccessKeyId,
        Guid depotId,
        Guid workspaceId,
        DateTimeOffset now)
    {
        DepotAccessKeyId = depotAccessKeyId;
        DepotId = depotId;
        WorkspaceId = workspaceId;
        CreatedAt = now;
    }

    public Guid DepotAccessKeyId { get; private set; }

    public Guid DepotId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DepotAccessKey? AccessKey { get; private set; }

    public Workspace? Workspace { get; private set; }
}
