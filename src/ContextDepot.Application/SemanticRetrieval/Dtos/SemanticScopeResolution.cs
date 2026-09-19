using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.SemanticRetrieval.Dtos;

public sealed record SemanticScopeResolution(
    ScopeResolution Resolution,
    IReadOnlySet<Guid>? WorkspaceIds);
