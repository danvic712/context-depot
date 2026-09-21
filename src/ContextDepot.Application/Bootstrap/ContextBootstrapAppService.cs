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

namespace ContextDepot.Application.Bootstrap;

public sealed class ContextBootstrapAppService : IContextBootstrapAppService
{
    private readonly ICurrentDepotContext currentDepot;
    private readonly IWorkspaceAccessContext workspaceAccess;
    private readonly IWorkspaceAppService workspaceAppService;
    private readonly IBootstrapRepository repository;
    private readonly ISemanticRetrievalRepository semanticRepository;
    private readonly EmbeddingGeneratorService embeddingGenerator;
    private readonly SemanticFallbackDecider semanticFallbackDecider;
    private readonly SemanticWorkspaceAggregator semanticWorkspaceAggregator;
    private readonly HybridCandidateRanker hybridCandidateRanker;
    private readonly RetrievalDeduplicator retrievalDeduplicator;
    private readonly TimeProvider timeProvider;
    private readonly WorkspaceScopeResolver scopeResolver;
    private readonly ScopeCandidateRanker candidateRanker;
    private readonly BootstrapResultSelector resultSelector;
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
        ContextBudgetAllocator contextBudgetAllocator,
        TimeProvider timeProvider,
        ILogger<ContextBootstrapAppService> logger)
    {
        this.currentDepot = currentDepot;
        this.workspaceAccess = workspaceAccess;
        this.workspaceAppService = workspaceAppService;
        this.repository = repository;
        this.semanticRepository = semanticRepository;
        this.embeddingGenerator = embeddingGenerator;
        this.semanticFallbackDecider = semanticFallbackDecider;
        this.semanticWorkspaceAggregator = semanticWorkspaceAggregator;
        this.hybridCandidateRanker = hybridCandidateRanker;
        this.retrievalDeduplicator = retrievalDeduplicator;
        this.timeProvider = timeProvider;
        this.logger = logger;
        candidateRanker = new ScopeCandidateRanker();
        scopeResolver = new WorkspaceScopeResolver(candidateRanker);
        resultSelector = new BootstrapResultSelector(contextBudgetAllocator);
    }

    public async Task<BootstrapResult> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken)
    {
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
            var semantic = await TrySearchSemanticAsync(
                queryEmbeddingCache,
                permittedWorkspaceIds,
                semanticFallbackDecider.ScopeTopK,
                query,
                bootstrapQuery.Now,
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

        var lexicalContextScores = contexts
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .ToDictionary(
                x => x.Id,
                x => candidateRanker.ScoreContext(
                    x,
                    workspacePaths.GetValueOrDefault(x.WorkspaceId, string.Empty),
                    query,
                    queryTokens));
        var lexicalDocumentScores = chunks
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .ToDictionary(
                x => x.Id,
                x => candidateRanker.ScoreDocument(
                    x,
                    workspacePaths.GetValueOrDefault(x.WorkspaceId, string.Empty),
                    query,
                    queryTokens));
        var hasLexicalCandidates = lexicalContextScores.Values.Any(score => score > 0) ||
                                   lexicalDocumentScores.Values.Any(score => score > 0);
        var topLexicalScore = lexicalContextScores.Values
            .Concat(lexicalDocumentScores.Values)
            .DefaultIfEmpty(0)
            .Max();
        var shouldUseSemanticRetrieval = scopeIds is not { Count: 0 } &&
            (semanticUsed || semanticFallbackDecider.ShouldUseForRetrieval(
                queryTokens.Count > 0,
                hasLexicalCandidates,
                topLexicalScore));
        if (shouldUseSemanticRetrieval)
        {
            var semantic = await TrySearchSemanticAsync(
                queryEmbeddingCache,
                scopeIds,
                semanticFallbackDecider.CandidateTopKPerSource,
                query,
                bootstrapQuery.Now,
                cancellationToken);
            if (semantic.Contexts.Count > 0 || semantic.Documents.Count > 0)
            {
                semanticContexts = semantic.Contexts.ToArray();
                semanticDocuments = semantic.Documents.ToArray();
            }

            semanticUsed |= semantic.Used;
            retrievalDegraded |= semantic.Degraded;
        }

        var contextCandidates = contexts.ToDictionary(x => x.Id);
        foreach (var candidate in semanticContexts)
        {
            contextCandidates[candidate.Context.Id] = candidate.Context;
        }

        var documentCandidates = chunks.ToDictionary(x => x.Id);
        foreach (var candidate in semanticDocuments)
        {
            documentCandidates[candidate.Document.Id] = candidate.Document;
        }

        var semanticContextScores = semanticContexts
            .GroupBy(x => x.ContextItemId)
            .ToDictionary(x => x.Key, x => x.Max(candidate => candidate.Similarity));
        var semanticDocumentScores = semanticDocuments
            .GroupBy(x => x.DocumentChunkId)
            .ToDictionary(x => x.Key, x => x.Max(candidate => candidate.Similarity));
        var broadQuery = scopeResolution.Status == ScopeResolutionStatus.Broad && queryTokens.Count > 0;
        var rankedContextCandidates = hybridCandidateRanker.RankContexts(
                contextCandidates.Values
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .ToArray(),
                query,
                workspacePaths,
                semanticContextScores)
            .Where(x => x.SemanticScore >= 0.65 || (!broadQuery
                ? queryTokens.Count == 0 || lexicalContextScores.GetValueOrDefault(x.Context.Id) - x.Context.Importance / 100d > 0
                : lexicalContextScores.GetValueOrDefault(x.Context.Id) - x.Context.Importance / 100d >= 2))
            .ToArray();
        var deduplicatedContexts = retrievalDeduplicator
            .DeduplicateContexts(rankedContextCandidates);
        var rankedContexts = deduplicatedContexts
            .Select(x => BootstrapModelMapper.ToContextModel(x.Context))
            .ToArray();

        var rankedDocumentCandidates = hybridCandidateRanker.RankDocuments(
                documentCandidates.Values
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .ToArray(),
                query,
                workspacePaths,
                semanticDocumentScores)
            .Where(x => x.SemanticScore >= 0.65 || (!broadQuery
                ? x.LexicalScore > 0 || queryTokens.Count == 0
                : lexicalDocumentScores.GetValueOrDefault(x.Document.Id) >= 2))
            .ToArray();
        var deduplicatedDocuments = retrievalDeduplicator
            .DeduplicateDocuments(rankedDocumentCandidates);
        var rankedDocuments = deduplicatedDocuments
            .Select(x => BootstrapModelMapper.ToDocumentExcerpt(x.Document, workspacePaths))
            .ToArray();

        var selection = resultSelector.Select(
            rankedContexts,
            rankedDocuments,
            request.MaxTokens,
            deduplicatedContexts.ToDictionary(x => x.Context.Id, x => x.Score),
            deduplicatedDocuments.ToDictionary(x => x.Document.Id, x => x.Score));
        return new BootstrapResult(
            scopeResolution,
            selection.Contexts,
            selection.Documents,
            new RetrievalDiagnostics(
                retrievalDegraded,
                semanticUsed,
                semanticUsed ? "hybrid" : retrievalDegraded ? "lexical-degraded" : "lexical"),
            selection.EstimatedTokens);
    }

    private async Task<(IReadOnlyList<SemanticContextCandidateRecord> Contexts,
        IReadOnlyList<SemanticDocumentCandidateRecord> Documents,
        bool Used,
        bool Degraded)> TrySearchSemanticAsync(
        QueryEmbeddingCache embeddingCache,
        IReadOnlySet<Guid>? workspaceIds,
        int topK,
        string query,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var queryVector = await embeddingCache.GetOrCreateAsync(query, cancellationToken);
            var semanticQuery = new SemanticCandidateQuery(
                currentDepot.DepotId,
                workspaceIds?.ToArray(),
                null,
                topK,
                now);
            var contexts = await semanticRepository.FindContextCandidatesAsync(
                semanticQuery,
                queryVector,
                cancellationToken);
            var documents = await semanticRepository.FindDocumentCandidatesAsync(
                semanticQuery,
                queryVector,
                cancellationToken);
            return (contexts, documents, true, false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (ContextDepotApplicationException exception) when (IsSemanticDegradation(exception.ErrorCode))
        {
            logger.LogWarning(
                exception,
                "{ErrorCode} degraded semantic retrieval for the current read request.",
                exception.ErrorCode);
            return ([], [], false, true);
        }
    }

    private static bool IsSemanticDegradation(string errorCode) =>
        errorCode is ApplicationErrorCodes.EmbeddingGeneratorUnavailable
            or ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse
            or ApplicationErrorCodes.EmbeddingDimensionMismatch
            or ApplicationErrorCodes.SecretContentRejected
            or ApplicationErrorCodes.VectorSearchFailed;
}
