using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.SemanticRetrieval.Dtos;

public sealed record SemanticCandidateQuery(
    IReadOnlyList<Guid>? WorkspaceIds,
    IReadOnlyList<ContextKind>? Kinds,
    int TopK,
    DateTimeOffset Now,
    int OversampleFactor = 3,
    bool IncludeDocuments = true);
