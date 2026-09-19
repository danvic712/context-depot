using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.Contexts.Dtos;

public sealed record ContextSearchResult(
    IReadOnlyList<ContextSearchMatch> Contexts,
    IReadOnlyList<DocumentSearchMatch> Documents,
    RetrievalDiagnostics Retrieval);
