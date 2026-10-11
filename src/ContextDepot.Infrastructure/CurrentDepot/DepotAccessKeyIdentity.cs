namespace ContextDepot.Infrastructure.CurrentDepot;

public sealed record DepotAccessKeyIdentity(
    Guid DepotAccessKeyId,
    Guid DepotId,
    string DepotDisplayName,
    IReadOnlyList<Guid> WorkspaceIds,
    IReadOnlyList<Guid> NavigableWorkspaceIds,
    bool CanCreateRootWorkspaces = false,
    long AccessVersion = 0);
