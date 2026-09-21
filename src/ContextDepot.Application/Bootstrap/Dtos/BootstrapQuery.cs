namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapQuery(
    Guid DepotId,
    IReadOnlySet<Guid>? WorkspaceIds,
    DateTimeOffset Now,
    int CandidateLimit = 5_000);
