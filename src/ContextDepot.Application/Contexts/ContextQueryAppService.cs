using System.Text.Json;
using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Documents.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Retrieval;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Application.Retrieval.Enums;
using ContextDepot.Application.SemanticRetrieval;
using ContextDepot.Application.SemanticRetrieval.Contracts;
using ContextDepot.Application.SemanticRetrieval.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SearchMatchType = ContextDepot.Application.Retrieval.Enums.MatchType;

namespace ContextDepot.Application.Contexts;

public sealed class ContextQueryAppService : IContextQueryAppService
{
    private readonly ICurrentDepotContext currentDepot;
    private readonly IWorkspaceAccessContext workspaceAccess;
    private readonly IContextQueryRepository repository;
    private readonly IWorkspaceAppService workspaceAppService;
    private readonly ISemanticRetrievalRepository semanticRepository;
    private readonly EmbeddingGeneratorService embeddingGenerator;
    private readonly SemanticFallbackDecider semanticFallbackDecider;
    private readonly HybridCandidateRanker hybridCandidateRanker;
    private readonly RetrievalDeduplicator retrievalDeduplicator;
    private readonly RetrievalOptions retrievalOptions;
    private readonly TimeProvider timeProvider;
    private readonly ILogger<ContextQueryAppService> logger;

    public ContextQueryAppService(
        ICurrentDepotContext currentDepot,
        IWorkspaceAccessContext workspaceAccess,
        IContextQueryRepository repository,
        IWorkspaceAppService workspaceAppService,
        ISemanticRetrievalRepository semanticRepository,
        EmbeddingGeneratorService embeddingGenerator,
        SemanticFallbackDecider semanticFallbackDecider,
        HybridCandidateRanker hybridCandidateRanker,
        RetrievalDeduplicator retrievalDeduplicator,
        IOptions<RetrievalOptions> retrievalOptions,
        TimeProvider timeProvider,
        ILogger<ContextQueryAppService> logger)
    {
        this.currentDepot = currentDepot;
        this.workspaceAccess = workspaceAccess;
        this.repository = repository;
        this.workspaceAppService = workspaceAppService;
        this.semanticRepository = semanticRepository;
        this.embeddingGenerator = embeddingGenerator;
        this.semanticFallbackDecider = semanticFallbackDecider;
        this.hybridCandidateRanker = hybridCandidateRanker;
        this.retrievalDeduplicator = retrievalDeduplicator;
        this.retrievalOptions = retrievalOptions.Value;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<ContextSearchResult> SearchAsync(
        ContextSearchRequest request,
        CancellationToken cancellationToken)
    {
        var query = ValidateAndNormalize(request, out var limit);
        var workspaceScope = await ResolveWorkspaceScopeAsync(request, cancellationToken);
        var searchQuery = new ContextSearchQuery(
            currentDepot.DepotId,
            workspaceScope.Ids,
            request.Kinds,
            query,
            timeProvider.GetUtcNow(),
            Math.Min(1_000, Math.Max(limit * 10, 100)));
        var lexicalContexts = await repository.FindLexicalContextCandidatesAsync(searchQuery, cancellationToken);
        var lexicalDocuments = await repository.FindLexicalDocumentCandidatesAsync(searchQuery, cancellationToken);
        var workspacePaths = new Dictionary<Guid, string>(workspaceScope.Paths);
        foreach (var candidate in lexicalContexts)
        {
            workspacePaths.TryAdd(candidate.WorkspaceId, candidate.WorkspacePath);
        }

        foreach (var candidate in lexicalDocuments)
        {
            workspacePaths.TryAdd(candidate.WorkspaceId, candidate.WorkspacePath);
        }
        await AddMissingWorkspacePathsAsync(
            lexicalContexts.Select(x => x.WorkspaceId)
                .Concat(lexicalDocuments.Select(x => x.WorkspaceId))
                .Distinct()
                .Where(workspaceId => !workspacePaths.ContainsKey(workspaceId)),
            workspacePaths,
            cancellationToken);

        var lexicalContextCandidates = lexicalContexts
            .GroupBy(x => x.ContextItemId)
            .ToDictionary(x => x.Key, x => x.First().Context);
        var lexicalDocumentCandidates = lexicalDocuments
            .GroupBy(x => x.DocumentChunkId)
            .ToDictionary(x => x.Key, x => x.First().Document);
        var scopeRanker = new ScopeCandidateRanker();
        var queryTokens = BootstrapQueryTokenizer.Tokenize(query);
        var lexicalContextScores = lexicalContextCandidates.Values.ToDictionary(
            candidate => candidate.Id,
            candidate => scopeRanker.ScoreContext(
                candidate,
                workspacePaths.GetValueOrDefault(candidate.WorkspaceId, string.Empty),
                query,
                queryTokens));
        var lexicalDocumentScores = lexicalDocumentCandidates.Values.ToDictionary(
            candidate => candidate.Id,
            candidate => scopeRanker.ScoreDocument(
                candidate,
                workspacePaths.GetValueOrDefault(candidate.WorkspaceId, string.Empty),
                query,
                queryTokens));
        var lexicalContextSignals = lexicalContextCandidates.Values.ToDictionary(
            candidate => candidate.Id,
            candidate => lexicalContextScores.GetValueOrDefault(candidate.Id) - candidate.Importance / 100d);
        var lexicalDocumentSignals = lexicalDocumentScores;
        var hasLexicalCandidates = lexicalContextSignals.Values.Any(score => score > 0) ||
                                   lexicalDocumentSignals.Values.Any(score => score > 0);
        var topLexicalScore = lexicalContextSignals.Values
            .Concat(lexicalDocumentSignals.Values)
            .Select(score => Math.Clamp(score / 10d, 0, 1))
            .DefaultIfEmpty(0)
            .Max();
        var semanticContexts = Array.Empty<SemanticContextCandidateRecord>();
        var semanticDocuments = Array.Empty<SemanticDocumentCandidateRecord>();
        var semanticUsed = false;
        var retrievalDegraded = false;
        if (semanticFallbackDecider.ShouldUseForRetrieval(
                hasQuerySignal: query.Length > 0,
                hasLexicalCandidates,
                topLexicalScore))
        {
            var semantic = await TrySearchSemanticAsync(
                workspaceScope.Ids,
                request.Kinds,
                query,
                searchQuery.Now,
                cancellationToken);
            semanticContexts = semantic.Contexts.ToArray();
            semanticDocuments = semantic.Documents.ToArray();
            semanticUsed = semantic.Used;
            retrievalDegraded = semantic.Degraded;
            await AddMissingWorkspacePathsAsync(
                semanticContexts.Select(x => x.WorkspaceId)
                    .Concat(semanticDocuments.Select(x => x.WorkspaceId))
                    .Distinct()
                    .Where(workspaceId => !workspacePaths.ContainsKey(workspaceId)),
                workspacePaths,
                cancellationToken);
        }

        foreach (var candidate in semanticContexts)
        {
            lexicalContextCandidates[candidate.ContextItemId] = candidate.Context;
        }

        foreach (var candidate in semanticDocuments)
        {
            lexicalDocumentCandidates[candidate.DocumentChunkId] = candidate.Document;
        }

        var semanticContextScores = semanticContexts
            .GroupBy(candidate => candidate.ContextItemId)
            .ToDictionary(group => group.Key, group => group.Max(candidate => candidate.Similarity));
        var semanticDocumentScores = semanticDocuments
            .GroupBy(candidate => candidate.DocumentChunkId)
            .ToDictionary(group => group.Key, group => group.Max(candidate => candidate.Similarity));
        var rankedContexts = hybridCandidateRanker
            .RankContexts(
                lexicalContextCandidates.Values.ToArray(),
                query,
                workspacePaths,
                semanticContextScores)
            .Where(candidate => lexicalContextSignals.GetValueOrDefault(candidate.Context.Id) > 0 ||
                                candidate.SemanticScore >= 0.65)
            .ToArray();
        var rankedDocuments = hybridCandidateRanker
            .RankDocuments(
                lexicalDocumentCandidates.Values.ToArray(),
                query,
                workspacePaths,
                semanticDocumentScores)
            .Where(candidate => lexicalDocumentSignals.GetValueOrDefault(candidate.Document.Id) > 0 ||
                                candidate.SemanticScore >= 0.65)
            .ToArray();
        var deduplicatedContexts = retrievalDeduplicator.DeduplicateContexts(rankedContexts);
        var deduplicatedDocuments = retrievalDeduplicator.DeduplicateDocuments(rankedDocuments);
        var matches = new List<(double Score, ContextSearchMatch? Context, DocumentSearchMatch? Document)>();
        matches.AddRange(deduplicatedContexts.Select(candidate =>
            (candidate.Score,
                (ContextSearchMatch?)ToContextMatch(candidate, workspacePaths),
                (DocumentSearchMatch?)null)));
        matches.AddRange(deduplicatedDocuments.Select(candidate =>
            (candidate.Score,
                (ContextSearchMatch?)null,
                (DocumentSearchMatch?)ToDocumentMatch(candidate, workspacePaths))));
        var selected = matches
            .OrderByDescending(match => match.Score)
            .ThenBy(match => match.Context is null ? 1 : 0)
            .Take(limit)
            .ToArray();
        return new ContextSearchResult(
            selected.Where(match => match.Context is not null).Select(match => match.Context!).ToArray(),
            selected.Where(match => match.Document is not null).Select(match => match.Document!).ToArray(),
            new RetrievalDiagnostics(
                retrievalDegraded,
                semanticUsed,
                semanticUsed ? "hybrid" : retrievalDegraded ? "lexical-degraded" : "lexical"));
    }

    public async Task<ContextDetailModel?> GetAsync(
        Guid contextId,
        CancellationToken cancellationToken)
    {
        var context = await repository.FindContextByIdAsync(currentDepot.DepotId, contextId, cancellationToken);
        if (context is null)
        {
            return null;
        }

        var workspace = await workspaceAppService.GetAsync(context.WorkspaceId, cancellationToken);
        return workspace is null ? null : ToDetailModel(context, workspace.Path);
    }

    private async Task<(HashSet<Guid>? Ids, Dictionary<Guid, string> Paths)> ResolveWorkspaceScopeAsync(
        ContextSearchRequest request,
        CancellationToken cancellationToken)
    {
        if (request.Workspaces is not { Count: > 0 })
        {
            return workspaceAccess.HasUnrestrictedAccess
                ? (null, new Dictionary<Guid, string>())
                : (new HashSet<Guid>(workspaceAccess.WorkspaceIds), new Dictionary<Guid, string>());
        }

        var ids = new HashSet<Guid>();
        var paths = new Dictionary<Guid, string>();
        foreach (var path in request.Workspaces)
        {
            var workspace = await workspaceAppService.ResolveAsync(path, cancellationToken)
                ?? throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
            ids.Add(workspace.Id);
            paths[workspace.Id] = workspace.Path;
            if (request.IncludeDescendants)
            {
                await AddDescendantsAsync(workspace.Path, ids, paths, cancellationToken);
            }
        }

        return (ids, paths);
    }

    private async Task AddDescendantsAsync(
        string parentPath,
        ISet<Guid> ids,
        IDictionary<Guid, string> paths,
        CancellationToken cancellationToken)
    {
        var pending = new Queue<string>([parentPath]);
        var visited = new HashSet<string>(StringComparer.Ordinal);
        while (pending.Count > 0)
        {
            var currentPath = pending.Dequeue();
            if (!visited.Add(currentPath))
            {
                continue;
            }

            var children = await workspaceAppService.ListAsync(currentPath, cancellationToken);
            foreach (var child in children)
            {
                ids.Add(child.Id);
                paths[child.Id] = child.Path;
                pending.Enqueue(child.Path);
            }
        }
    }

    private async Task AddMissingWorkspacePathsAsync(
        IEnumerable<Guid> workspaceIds,
        IDictionary<Guid, string> workspacePaths,
        CancellationToken cancellationToken)
    {
        foreach (var workspaceId in workspaceIds.Distinct())
        {
            var workspace = await workspaceAppService.GetAsync(workspaceId, cancellationToken);
            if (workspace is not null)
            {
                workspacePaths[workspaceId] = workspace.Path;
            }
        }
    }

    private async Task<(IReadOnlyList<SemanticContextCandidateRecord> Contexts,
        IReadOnlyList<SemanticDocumentCandidateRecord> Documents,
        bool Used,
        bool Degraded)> TrySearchSemanticAsync(
        IReadOnlySet<Guid>? workspaceIds,
        IReadOnlyList<ContextKind>? kinds,
        string query,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        try
        {
            var queryVector = await new QueryEmbeddingCache(embeddingGenerator)
                .GetOrCreateAsync(query, cancellationToken);
            var semanticQuery = new SemanticCandidateQuery(
                currentDepot.DepotId,
                workspaceIds?.ToArray(),
                kinds,
                semanticFallbackDecider.CandidateTopKPerSource,
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
                "{ErrorCode} degraded explicit context search.",
                exception.ErrorCode);
            return ([], [], false, true);
        }
    }

    private string ValidateAndNormalize(ContextSearchRequest request, out int limit)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length == 0 || query.Length > 4_000 || request.Workspaces is { Count: > 20 })
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSearchQuery);
        }

        limit = request.Limit ?? retrievalOptions.Search.DefaultLimit;
        if (limit <= 0 || limit > retrievalOptions.Search.MaxLimit)
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSearchQuery);
        }

        return query;
    }

    private static ContextSearchMatch ToContextMatch(
        RankedContextCandidate candidate,
        IReadOnlyDictionary<Guid, string> workspacePaths) =>
        new(
            candidate.Context.Id,
            workspacePaths.GetValueOrDefault(candidate.Context.WorkspaceId, string.Empty),
            candidate.Context.Kind,
            candidate.Context.Key,
            candidate.Context.Title,
            candidate.Context.Content,
            GetMatchType(candidate));

    private static DocumentSearchMatch ToDocumentMatch(
        RankedDocumentCandidate candidate,
        IReadOnlyDictionary<Guid, string> workspacePaths) =>
        new(
            candidate.Document.DocumentId,
            workspacePaths.GetValueOrDefault(candidate.Document.WorkspaceId, string.Empty),
            CombinePath(
                workspacePaths.GetValueOrDefault(candidate.Document.WorkspaceId, string.Empty),
                candidate.Document.Path),
            candidate.Document.Title,
            candidate.Document.HeadingPath,
            candidate.Document.Content,
            GetMatchType(candidate));

    private static SearchMatchType GetMatchType(RankedContextCandidate candidate) =>
        candidate.IsExactMatch
            ? SearchMatchType.Exact
            : candidate.SemanticScore > 0 && candidate.LexicalScore > 0
                ? SearchMatchType.Hybrid
                : candidate.SemanticScore > 0
                    ? SearchMatchType.Semantic
                    : SearchMatchType.Lexical;

    private static SearchMatchType GetMatchType(RankedDocumentCandidate candidate) =>
        candidate.IsExactMatch
            ? SearchMatchType.Exact
            : candidate.SemanticScore > 0 && candidate.LexicalScore > 0
                ? SearchMatchType.Hybrid
                : candidate.SemanticScore > 0
                    ? SearchMatchType.Semantic
                    : SearchMatchType.Lexical;

    private static string CombinePath(string workspacePath, string path) =>
        string.IsNullOrWhiteSpace(workspacePath)
            ? path
            : workspacePath.TrimEnd('/') + "/" + path.TrimStart('/');

    private static ContextDetailModel ToDetailModel(ContextItem context, string workspacePath)
    {
        var tags = JsonSerializer.Deserialize<string[]>(context.TagsJson) ?? [];
        return new ContextDetailModel(
            context.Id,
            context.DepotId,
            context.WorkspaceId,
            workspacePath,
            context.Kind,
            context.Key,
            context.Title,
            context.Content,
            tags,
            context.Importance,
            context.Confidence,
            context.Status,
            context.VerificationStatus,
            context.ProvenanceTrust,
            context.SourceType,
            context.SourceAgent,
            context.SourceRef,
            context.SupersedesId,
            context.ValidFrom,
            context.ValidUntil,
            context.ExpiresAt,
            context.Sensitivity,
            context.CreatedAt,
            context.UpdatedAt);
    }

    private static bool IsSemanticDegradation(string errorCode) =>
        errorCode is ApplicationErrorCodes.EmbeddingGeneratorUnavailable
            or ApplicationErrorCodes.EmbeddingGeneratorInvalidResponse
            or ApplicationErrorCodes.EmbeddingDimensionMismatch
            or ApplicationErrorCodes.SecretContentRejected
            or ApplicationErrorCodes.VectorSearchFailed;
}
