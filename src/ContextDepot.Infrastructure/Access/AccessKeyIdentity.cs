namespace ContextDepot.Infrastructure.Access;

public sealed record AccessKeyIdentity(
    Guid AccessKeyId,
    IReadOnlyList<Guid> WorkspaceIds,
    IReadOnlyList<Guid> NavigableWorkspaceIds,
    bool CanCreateRootWorkspaces = false,
    long AccessVersion = 0);
