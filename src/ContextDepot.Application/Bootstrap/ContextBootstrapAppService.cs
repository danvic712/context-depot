using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;

namespace ContextDepot.Application.Bootstrap;

public sealed class ContextBootstrapAppService : IContextBootstrapAppService
{
    private readonly ICurrentOwnerContext currentOwner;
    private readonly IWorkspaceAppService workspaceAppService;
    private readonly IBootstrapRepository repository;
    private readonly TimeProvider timeProvider;
    private readonly WorkspaceScopeResolver scopeResolver;
    private readonly ScopeCandidateRanker candidateRanker;
    private readonly BootstrapResultSelector resultSelector;

    public ContextBootstrapAppService(
        ICurrentOwnerContext currentOwner,
        IWorkspaceAppService workspaceAppService,
        IBootstrapRepository repository,
        TimeProvider timeProvider)
    {
        this.currentOwner = currentOwner;
        this.workspaceAppService = workspaceAppService;
        this.repository = repository;
        this.timeProvider = timeProvider;
        candidateRanker = new ScopeCandidateRanker();
        scopeResolver = new WorkspaceScopeResolver(candidateRanker);
        resultSelector = new BootstrapResultSelector(new SimpleTokenEstimator());
    }

    public async Task<BootstrapResult> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken)
    {
        if (request.MaxTokens is <= 0 or > 8_000)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextBudgetInvalid);
        }

        var query = request.Query?.Trim() ?? string.Empty;
        var queryTokens = BootstrapQueryTokenizer.Tokenize(query);
        var bootstrapQuery = new BootstrapQuery(currentOwner.OwnerId, timeProvider.GetUtcNow());
        var workspaces = await repository.FindScopeCandidatesAsync(bootstrapQuery, cancellationToken);
        var workspacePaths = scopeResolver.BuildPaths(workspaces);
        var contexts = await repository.FindContextCandidatesAsync(bootstrapQuery, cancellationToken);
        var chunks = await repository.FindDocumentCandidatesAsync(bootstrapQuery, cancellationToken);

        HashSet<Guid>? scopeIds;
        ScopeResolution scopeResolution;
        if (request.Workspaces is { Count: > 0 })
        {
            scopeIds = [];
            var resolvedPaths = new List<string>();
            foreach (var path in request.Workspaces)
            {
                var workspace = await workspaceAppService.ResolveAsync(path, cancellationToken)
                    ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
                scopeIds.Add(workspace.Id);
                resolvedPaths.Add(workspace.Path);
            }

            scopeResolution = new ScopeResolution(ScopeResolutionStatus.Resolved, resolvedPaths);
        }
        else
        {
            (scopeResolution, scopeIds) = scopeResolver.Resolve(queryTokens, workspaces, workspacePaths, contexts, chunks);
        }

        var broadQuery = scopeResolution.Status == ScopeResolutionStatus.Broad && queryTokens.Count > 0;
        var rankedContexts = contexts
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .Select(x => (Context: x, Score: candidateRanker.ScoreContext(x, workspacePaths.GetValueOrDefault(x.WorkspaceId, string.Empty), query, queryTokens)))
            .Where(x => !broadQuery
                ? queryTokens.Count == 0 || x.Score - x.Context.Importance / 100d > 0
                : x.Score - x.Context.Importance / 100d >= 2)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Context.Importance)
            .ThenByDescending(x => x.Context.UpdatedAt)
            .Select(x => BootstrapModelMapper.ToContextModel(x.Context))
            .ToArray();

        var rankedDocuments = chunks
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .Select(x => (Chunk: x, Score: candidateRanker.ScoreDocument(x, workspacePaths.GetValueOrDefault(x.WorkspaceId, string.Empty), query, queryTokens)))
            .Where(x => !broadQuery ? x.Score > 0 || queryTokens.Count == 0 : x.Score >= 2)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.Ordinal)
            .Select(x => BootstrapModelMapper.ToDocumentExcerpt(x.Chunk, workspacePaths))
            .ToArray();

        var selection = resultSelector.Select(rankedContexts, rankedDocuments, request.MaxTokens);
        return new BootstrapResult(
            scopeResolution,
            selection.Contexts,
            selection.Documents,
            new RetrievalDiagnostics(false, false, "lexical"),
            selection.EstimatedTokens);
    }
}
