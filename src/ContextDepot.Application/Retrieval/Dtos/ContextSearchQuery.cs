using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Retrieval.Dtos;

public sealed record ContextSearchQuery(
    IReadOnlySet<Guid>? WorkspaceIds,
    IReadOnlyList<ContextKind>? Kinds,
    string Query,
    DateTimeOffset Now,
    int CandidateLimit);
