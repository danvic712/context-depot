using ContextDepot.Application.Contexts.Dtos;

namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record BootstrapResult(
    ScopeResolution ScopeResolution,
    IReadOnlyList<ContextModel> Contexts,
    IReadOnlyList<DocumentExcerptModel> Documents,
    RetrievalDiagnostics Diagnostics,
    int EstimatedTokens);
