namespace ContextDepot.Infrastructure.CurrentDepot;

public sealed record DepotAccessKeyIdentity(
    Guid DepotAccessKeyId,
    Guid DepotId,
    string DepotDisplayName,
    IReadOnlyList<Guid> WorkspaceIds,
    IReadOnlyList<Guid> NavigableWorkspaceIds);
