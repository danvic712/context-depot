using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Contexts.Dtos;

public sealed record ContextSearchRequest(
    string Query,
    IReadOnlyList<string>? Workspaces = null,
    bool IncludeDescendants = false,
    IReadOnlyList<ContextKind>? Kinds = null,
    int? Limit = null);
