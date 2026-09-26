using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.SemanticRetrieval;
using ContextDepot.Application.SemanticRetrieval.Contracts;
using ContextDepot.Application.SemanticRetrieval.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Bootstrap;

public sealed class ContextBootstrapAppService : IContextBootstrapAppService
{
    private readonly ICurrentDepotContext currentDepot;
    private readonly IWorkspaceAccessContext workspaceAccess;
    private readonly IWorkspaceAppService workspaceAppService;
    private readonly IBootstrapRepository repository;
    private readonly EmbeddingGeneratorService embeddingGenerator;
    private readonly SemanticCandidateSearcher semanticSearcher;
    private readonly SemanticFallbackDecider semanticFallbackDecider;
    private readonly SemanticWorkspaceAggregator semanticWorkspaceAggregator;
    private readonly HybridCandidateRanker hybridCandidateRanker;
    private readonly RetrievalDeduplicator retrievalDeduplicator;
    private readonly IOptionsMonitor<RetrievalOptions> retrievalOptions;
    private readonly TimeProvider timeProvider;
    private readonly WorkspaceScopeResolver scopeResolver;
    private readonly ScopeCandidateRanker candidateRanker;
    private readonly ContextBudgetAllocator contextBudgetAllocator;
    private readonly ILogger<ContextBootstrapAppService> logger;

    public ContextBootstrapAppService(
        ICurrentDepotContext currentDepot,
        IWorkspaceAccessContext workspaceAccess,
        IWorkspaceAppService workspaceAppService,
        IBootstrapRepository repository,
        ISemanticRetrievalRepository semanticRepository,
        EmbeddingGeneratorService embeddingGenerator,
        SemanticFallbackDecider semanticFallbackDecider,
        SemanticWorkspaceAggregator semanticWorkspaceAggregator,
        HybridCandidateRanker hybridCandidateRanker,
        RetrievalDeduplicator retrievalDeduplicator,
        IOptionsMonitor<RetrievalOptions> retrievalOptions,
        ContextBudgetAllocator contextBudgetAllocator,
        TimeProvider timeProvider,
        ILogger<ContextBootstrapAppService> logger)
    {
        this.currentDepot = currentDepot;
        this.workspaceAccess = workspaceAccess;
        this.workspaceAppService = workspaceAppService;
        this.repository = repository;
        this.embeddingGenerator = embeddingGenerator;
        semanticSearcher = new SemanticCandidateSearcher(semanticRepository);
        this.semanticFallbackDecider = semanticFallbackDecider;
        this.semanticWorkspaceAggregator = semanticWorkspaceAggregator;
        this.hybridCandidateRanker = hybridCandidateRanker;
        this.retrievalDeduplicator = retrievalDeduplicator;
        this.retrievalOptions = retrievalOptions;
        this.timeProvider = timeProvider;
        this.logger = logger;
        candidateRanker = new ScopeCandidateRanker();
        scopeResolver = new WorkspaceScopeResolver(candidateRanker);
        this.contextBudgetAllocator = contextBudgetAllocator;
    }

    public async Task<BootstrapResult> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken)
    {
        var retrievalSettings = retrievalOptions.CurrentValue;
        if (request.MaxTokens is <= 0 or > 8_000)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.ContextBudgetInvalid);
        }

        var query = request.Query?.Trim() ?? string.Empty;
        var queryTokens = BootstrapQueryTokenizer.Tokenize(query);
        var permittedWorkspaceIds = workspaceAccess.HasUnrestrictedAccess
            ? null
            : new HashSet<Guid>(workspaceAccess.WorkspaceIds);
        var bootstrapQuery = new BootstrapQuery(
            currentDepot.DepotId,
            permittedWorkspaceIds,
            timeProvider.GetUtcNow());
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

        if (workspaceAccess.HasUnrestrictedAccess &&
            scopeResolution.Status == ScopeResolutionStatus.Broad &&
            scopeIds is { Count: 0 })
        {
            scopeIds = null;
        }
        else if (!workspaceAccess.HasUnrestrictedAccess && scopeIds is null)
        {
            scopeIds = permittedWorkspaceIds;
        }

        var queryEmbeddingCache = new QueryEmbeddingCache(embeddingGenerator);
        var semanticContexts = Array.Empty<SemanticContextCandidateRecord>();
        var semanticDocuments = Array.Empty<SemanticDocumentCandidateRecord>();
        var semanticUsed = false;
        var retrievalDegraded = false;
        var hasExplicitScope = request.Workspaces is { Count: > 0 };
        if (semanticFallbackDecider.ShouldUseForScope(query, hasExplicitScope, scopeResolution))
        {
            var semantic = await semanticSearcher.SearchAsync(
                new SemanticCandidateQuery(
                    currentDepot.DepotId,
                    permittedWorkspaceIds?.ToArray(),
                    null,
                    retrievalSettings.Semantic.ScopeTopK,
                    bootstrapQuery.Now,
                    retrievalSettings.Semantic.OversampleFactor),
                query,
                queryEmbeddingCache,
                logger,
                cancellationToken);
            semanticContexts = semantic.Contexts.ToArray();
            semanticDocuments = semantic.Documents.ToArray();
            semanticUsed |= semantic.Used;
            retrievalDegraded |= semantic.Degraded;
            if (semanticContexts.Length > 0 || semanticDocuments.Length > 0)
            {
                var semanticScope = semanticWorkspaceAggregator.Aggregate(
                    semanticContexts,
                    semanticDocuments,
                    workspacePaths);
                scopeResolution = semanticScope.Resolution;
                scopeIds = semanticScope.WorkspaceIds is null
                    ? permittedWorkspaceIds
                    : new HashSet<Guid>(semanticScope.WorkspaceIds);
            }
        }

        var lexical = RetrievalCandidateComposer.AnalyzeLexical(
            contexts,
            chunks,
            workspacePaths,
            query,
            queryTokens,
            scopeIds);
        var shouldUseSemanticRetrieval = scopeIds is not { Count: 0 } &&
            (semanticUsed || semanticFallbackDecider.ShouldUseForRetrieval(
                queryTokens.Count > 0,
                lexical.HasCandidates,
                lexical.TopScore,
                retrievalSettings.Semantic));
        if (shouldUseSemanticRetrieval)
        {
            var semantic = await semanticSearcher.SearchAsync(
                new SemanticCandidateQuery(
                    currentDepot.DepotId,
                    scopeIds?.ToArray(),
                    null,
                    retrievalSettings.Semantic.CandidateTopKPerSource,
                    bootstrapQuery.Now,
                    retrievalSettings.Semantic.OversampleFactor),
                query,
                queryEmbeddingCache,
                logger,
                cancellationToken);
            if (semantic.Contexts.Count > 0 || semantic.Documents.Count > 0)
            {
                semanticContexts = semantic.Contexts.ToArray();
                semanticDocuments = semantic.Documents.ToArray();
            }

            semanticUsed |= semantic.Used;
            retrievalDegraded |= semantic.Degraded;
        }

        var broadQuery = scopeResolution.Status == ScopeResolutionStatus.Broad && queryTokens.Count > 0;
        var composed = RetrievalCandidateComposer.Compose(
            new RetrievalCandidateSources(contexts, chunks, semanticContexts, semanticDocuments),
            lexical,
            new RetrievalCompositionSettings(
                workspacePaths,
                query,
                retrievalSettings.Semantic,
                new CandidateSelection(
                    broadQuery ? CandidateSelectionKind.BootstrapBroad : CandidateSelectionKind.Bootstrap,
                    queryTokens.Count > 0),
                scopeIds,
                semanticUsed,
                retrievalDegraded),
            hybridCandidateRanker,
            retrievalDeduplicator);
        var rankedContexts = composed.Contexts
            .Select(x => BootstrapModelMapper.ToContextModel(x.Context))
            .ToArray();
        var rankedDocuments = composed.Documents
            .Select(x => BootstrapModelMapper.ToDocumentExcerpt(x.Document, workspacePaths))
            .ToArray();

        var selection = contextBudgetAllocator.Allocate(
            rankedContexts,
            rankedDocuments,
            request.MaxTokens,
            composed.Contexts.ToDictionary(x => x.Context.Id, x => x.Score),
            composed.Documents.ToDictionary(x => x.Document.Id, x => x.Score));
        return new BootstrapResult(
            scopeResolution,
            selection.Contexts,
            selection.Documents,
            composed.Diagnostics,
            selection.EstimatedTokens);
    }

}
