namespace ContextDepot.Infrastructure.HealthChecks;

public sealed record DocumentCoverageSource(
    Guid Id,
    Guid DepotId,
    Guid WorkspaceId,
    string Path,
    string Title,
    string HeadingPath,
    string Content);
