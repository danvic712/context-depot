using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Embeddings;
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
    private readonly ICurrentOwnerContext currentOwner;
    private readonly IWorkspaceAppService workspaceAppService;
    private readonly IBootstrapRepository repository;
    private readonly ISemanticRetrievalRepository semanticRepository;
    private readonly EmbeddingGeneratorService embeddingGenerator;
    private readonly SemanticFallbackDecider semanticFallbackDecider;
    private readonly SemanticWorkspaceAggregator semanticWorkspaceAggregator;
    private readonly TimeProvider timeProvider;
    private readonly WorkspaceScopeResolver scopeResolver;
    private readonly ScopeCandidateRanker candidateRanker;
    private readonly BootstrapResultSelector resultSelector;
    private readonly ILogger<ContextBootstrapAppService> logger;

    public ContextBootstrapAppService(
        ICurrentOwnerContext currentOwner,
        IWorkspaceAppService workspaceAppService,
        IBootstrapRepository repository,
        ISemanticRetrievalRepository semanticRepository,
        EmbeddingGeneratorService embeddingGenerator,
        SemanticFallbackDecider semanticFallbackDecider,
        SemanticWorkspaceAggregator semanticWorkspaceAggregator,
        TimeProvider timeProvider,
        ILogger<ContextBootstrapAppService> logger)
    {
        this.currentOwner = currentOwner;
        this.workspaceAppService = workspaceAppService;
        this.repository = repository;
        this.semanticRepository = semanticRepository;
        this.embeddingGenerator = embeddingGenerator;
        this.semanticFallbackDecider = semanticFallbackDecider;
        this.semanticWorkspaceAggregator = semanticWorkspaceAggregator;
        this.timeProvider = timeProvider;
        this.logger = logger;
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

        if (scopeResolution.Status == ScopeResolutionStatus.Broad && scopeIds is { Count: 0 })
        {
            scopeIds = null;
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
                null,
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
                    ? null
                    : new HashSet<Guid>(semanticScope.WorkspaceIds);
            }
        }

        var lexicalContextScores = contexts
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .Select(x => candidateRanker.ScoreContext(
                x,
                workspacePaths.GetValueOrDefault(x.WorkspaceId, string.Empty),
                query,
                queryTokens))
            .ToArray();
        var lexicalDocumentScores = chunks
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .Select(x => candidateRanker.ScoreDocument(
                x,
                workspacePaths.GetValueOrDefault(x.WorkspaceId, string.Empty),
                query,
                queryTokens))
            .ToArray();
        var hasLexicalCandidates = lexicalContextScores.Any(score => score > 0) ||
                                   lexicalDocumentScores.Any(score => score > 0);
        var topLexicalScore = lexicalContextScores
            .Concat(lexicalDocumentScores)
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

        var semanticContextScores = semanticContexts.ToDictionary(x => x.ContextItemId, x => x.Similarity);
        var semanticDocumentScores = semanticDocuments.ToDictionary(x => x.DocumentChunkId, x => x.Similarity);
        var broadQuery = scopeResolution.Status == ScopeResolutionStatus.Broad && queryTokens.Count > 0;
        var rankedContexts = contextCandidates.Values
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .Select(x =>
            {
                var lexicalScore = candidateRanker.ScoreContext(
                    x,
                    workspacePaths.GetValueOrDefault(x.WorkspaceId, string.Empty),
                    query,
                    queryTokens);
                var semanticScore = semanticContextScores.GetValueOrDefault(x.Id);
                return (Context: x, LexicalScore: lexicalScore, SemanticScore: semanticScore, Score: lexicalScore + semanticScore * 2);
            })
            .Where(x => x.SemanticScore >= 0.65 || (!broadQuery
                ? queryTokens.Count == 0 || x.LexicalScore - x.Context.Importance / 100d > 0
                : x.LexicalScore - x.Context.Importance / 100d >= 2))
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Context.Importance)
            .ThenByDescending(x => x.Context.UpdatedAt)
            .Select(x => BootstrapModelMapper.ToContextModel(x.Context))
            .ToArray();

        var rankedDocuments = documentCandidates.Values
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .Select(x =>
            {
                var lexicalScore = candidateRanker.ScoreDocument(
                    x,
                    workspacePaths.GetValueOrDefault(x.WorkspaceId, string.Empty),
                    query,
                    queryTokens);
                var semanticScore = semanticDocumentScores.GetValueOrDefault(x.Id);
                return (Chunk: x, LexicalScore: lexicalScore, SemanticScore: semanticScore, Score: lexicalScore + semanticScore * 2);
            })
            .Where(x => x.SemanticScore >= 0.65 || (!broadQuery
                ? x.LexicalScore > 0 || queryTokens.Count == 0
                : x.LexicalScore >= 2))
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.Ordinal)
            .Select(x => BootstrapModelMapper.ToDocumentExcerpt(x.Chunk, workspacePaths))
            .ToArray();

        var selection = resultSelector.Select(rankedContexts, rankedDocuments, request.MaxTokens);
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
                currentOwner.OwnerId,
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
