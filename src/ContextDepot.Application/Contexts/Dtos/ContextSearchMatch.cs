using ContextDepot.Application.Retrieval.Enums;
using System.Text.Json.Serialization;
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
    SearchMatchType MatchType,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] int? Ordinal = null);
