namespace ContextDepot.Infrastructure.Dtos;

public sealed record DepotAccessKeyIdentity(
    Guid DepotAccessKeyId,
    Guid DepotId,
    string DepotDisplayName,
    IReadOnlyList<Guid> WorkspaceIds,
    IReadOnlyList<Guid> NavigableWorkspaceIds);
