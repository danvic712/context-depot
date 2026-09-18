using ContextDepot.Application.Contexts;

namespace ContextDepot.Application.Retrieval;

public sealed record BootstrapRequest(
    string Query,
    IReadOnlyList<string>? Workspaces = null,
    int MaxTokens = 1200);

public enum ScopeResolutionStatus
{
    Resolved,
    Ambiguous,
    Broad
}

public sealed record ScopeResolution(ScopeResolutionStatus Status, IReadOnlyList<string> Workspaces);

public sealed record DocumentExcerptModel(
    Guid DocumentId,
    Guid ChunkId,
    string Path,
    string HeadingPath,
    string Content,
    string ContentHash);

public sealed record RetrievalDiagnostics(
    bool RetrievalDegraded,
    bool SemanticUsed,
    string Mode);

public sealed record BootstrapResult(
    ScopeResolution ScopeResolution,
    IReadOnlyList<ContextModel> Contexts,
    IReadOnlyList<DocumentExcerptModel> Documents,
    RetrievalDiagnostics Diagnostics,
    int EstimatedTokens);

public interface IContextBootstrapAppService
{
    Task<BootstrapResult> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken);
}
