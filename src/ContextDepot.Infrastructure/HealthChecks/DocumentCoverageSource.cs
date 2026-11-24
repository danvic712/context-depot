namespace ContextDepot.Infrastructure.HealthChecks;

public sealed record DocumentCoverageSource(
    Guid Id,
    Guid WorkspaceId,
    string Path,
    string Title,
    string HeadingPath,
    string Content);
