namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapQuery(Guid OwnerId, DateTimeOffset Now, int CandidateLimit = 5_000);
