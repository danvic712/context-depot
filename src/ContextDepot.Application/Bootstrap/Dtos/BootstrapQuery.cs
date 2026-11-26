namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapQuery(
    IReadOnlySet<Guid>? WorkspaceIds,
    DateTimeOffset Now,
    int CandidateLimit = 5_000,
    string Query = "");
