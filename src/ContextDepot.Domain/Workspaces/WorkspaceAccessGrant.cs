using ContextDepot.Domain.Access;

namespace ContextDepot.Domain.Workspaces;

public sealed class WorkspaceAccessGrant
{
    private WorkspaceAccessGrant()
    {
    }

    public WorkspaceAccessGrant(
        Guid accessKeyId,
        Guid workspaceId,
        DateTimeOffset now)
    {
        AccessKeyId = accessKeyId;
        WorkspaceId = workspaceId;
        CreatedAt = now;
    }

    public Guid AccessKeyId { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public AccessKey? AccessKey { get; private set; }

    public Workspace? Workspace { get; private set; }
}
