using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.SemanticRetrieval.Dtos;

public sealed record SemanticCandidateQuery(
    Guid DepotId,
    IReadOnlyList<Guid>? WorkspaceIds,
    IReadOnlyList<ContextKind>? Kinds,
    int TopK,
    DateTimeOffset Now,
    int OversampleFactor = 3);
