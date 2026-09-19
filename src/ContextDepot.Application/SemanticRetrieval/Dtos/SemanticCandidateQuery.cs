using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.SemanticRetrieval.Dtos;

public sealed record SemanticCandidateQuery(
    Guid OwnerId,
    IReadOnlyList<Guid>? WorkspaceIds,
    IReadOnlyList<ContextKind>? Kinds,
    int TopK,
    DateTimeOffset Now);
