using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Domain.Access;

public sealed class AccessKey
{
    private AccessKey()
    {
    }

    public AccessKey(
        Guid id,
        string name,
        string keyPrefix,
        string secretHash,
        DateTimeOffset now,
        bool canCreateRootWorkspaces = false)
    {
        Id = id;
        Name = name;
        KeyPrefix = keyPrefix;
        SecretHash = secretHash;
        CreatedAt = now;
        CanCreateRootWorkspaces = canCreateRootWorkspaces;
    }

    public Guid Id { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string KeyPrefix { get; private set; } = string.Empty;

    public string SecretHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public ICollection<WorkspaceAccessGrant> WorkspaceGrants { get; } = new List<WorkspaceAccessGrant>();

    public bool IsActive => RevokedAt is null;

    public bool CanCreateRootWorkspaces { get; private set; }

    public long AccessVersion { get; private set; }

    public void ChangeCreationCapability(bool enabled) => CanCreateRootWorkspaces = enabled;

    public void AdvanceAccessVersion() => AccessVersion = checked(AccessVersion + 1);

    public void MarkUsed(DateTimeOffset now)
    {
        LastUsedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        if (RevokedAt is not null) return;
        RevokedAt = now;
        AdvanceAccessVersion();
    }
}
