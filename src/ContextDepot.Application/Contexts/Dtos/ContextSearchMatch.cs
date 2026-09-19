using ContextDepot.Application.Retrieval.Enums;
using ContextDepot.Domain.Contexts.Enums;
using SearchMatchType = ContextDepot.Application.Retrieval.Enums.MatchType;

namespace ContextDepot.Application.Contexts.Dtos;

public sealed record ContextSearchMatch(
    Guid ContextId,
    string Workspace,
    ContextKind Kind,
    string? Key,
    string? Title,
    string Content,
    SearchMatchType MatchType);
