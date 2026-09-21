using ContextDepot.Domain.Workspaces;

namespace ContextDepot.Domain.Depots;

public sealed class DepotAccessKey
{
    private DepotAccessKey()
    {
    }

    public DepotAccessKey(
        Guid id,
        Guid depotId,
        string name,
        string keyPrefix,
        string secretHash,
        DateTimeOffset now)
    {
        Id = id;
        DepotId = depotId;
        Name = name;
        KeyPrefix = keyPrefix;
        SecretHash = secretHash;
        CreatedAt = now;
    }

    public Guid Id { get; private set; }

    public Guid DepotId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string KeyPrefix { get; private set; } = string.Empty;

    public string SecretHash { get; private set; } = string.Empty;

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset? LastUsedAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Depot? Depot { get; private set; }

    public ICollection<WorkspaceAccessGrant> WorkspaceGrants { get; } = new List<WorkspaceAccessGrant>();

    public bool IsActive => RevokedAt is null;

    public void MarkUsed(DateTimeOffset now)
    {
        LastUsedAt = now;
    }

    public void Revoke(DateTimeOffset now)
    {
        RevokedAt ??= now;
    }
}
