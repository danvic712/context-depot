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
    private readonly IContextQueryRepository repository;
    private readonly IWorkspaceAppService workspaceAppService;
    private readonly ContextSearchScopeResolver scopeResolver;
    private readonly EmbeddingGeneratorService embeddingGenerator;
    private readonly SemanticCandidateSearcher semanticSearcher;
    private readonly SemanticFallbackDecider semanticFallbackDecider;
    private readonly HybridCandidateRanker hybridCandidateRanker;
    private readonly RetrievalDeduplicator retrievalDeduplicator;
    private readonly IOptionsMonitor<RetrievalOptions> retrievalOptions;
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
        IOptionsMonitor<RetrievalOptions> retrievalOptions,
        TimeProvider timeProvider,
        ILogger<ContextQueryAppService> logger)
    {
        this.currentDepot = currentDepot;
        this.repository = repository;
        this.workspaceAppService = workspaceAppService;
        scopeResolver = new ContextSearchScopeResolver(workspaceAccess, workspaceAppService);
        this.embeddingGenerator = embeddingGenerator;
        semanticSearcher = new SemanticCandidateSearcher(semanticRepository);
        this.semanticFallbackDecider = semanticFallbackDecider;
        this.hybridCandidateRanker = hybridCandidateRanker;
        this.retrievalDeduplicator = retrievalDeduplicator;
        this.retrievalOptions = retrievalOptions;
        this.timeProvider = timeProvider;
        this.logger = logger;
    }

    public async Task<ContextSearchResult> SearchAsync(
        ContextSearchRequest request,
        CancellationToken cancellationToken)
    {
        var options = retrievalOptions.CurrentValue;
        var query = ValidateAndNormalize(request, options, out var limit);
        var workspaceScope = await scopeResolver.ResolveAsync(request, cancellationToken);
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
        await scopeResolver.AddMissingPathsAsync(
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
        var queryTokens = BootstrapQueryTokenizer.Tokenize(query);
        var lexical = RetrievalCandidateComposer.AnalyzeLexical(
            lexicalContextCandidates.Values.ToArray(),
            lexicalDocumentCandidates.Values.ToArray(),
            workspacePaths,
            query,
            queryTokens);
        var semanticContexts = Array.Empty<SemanticContextCandidateRecord>();
        var semanticDocuments = Array.Empty<SemanticDocumentCandidateRecord>();
        var semanticUsed = false;
        var retrievalDegraded = false;
        if (semanticFallbackDecider.ShouldUseForRetrieval(
                hasQuerySignal: query.Length > 0,
                lexical.HasCandidates,
                lexical.TopScore,
                options.Semantic))
        {
            var semantic = await semanticSearcher.SearchAsync(
                new SemanticCandidateQuery(
                    currentDepot.DepotId,
                    workspaceScope.Ids?.ToArray(),
                    request.Kinds,
                    options.Semantic.CandidateTopKPerSource,
                    searchQuery.Now,
                    options.Semantic.OversampleFactor),
                query,
                new QueryEmbeddingCache(embeddingGenerator),
                logger,
                cancellationToken);
            semanticContexts = semantic.Contexts.ToArray();
            semanticDocuments = semantic.Documents.ToArray();
            semanticUsed = semantic.Used;
            retrievalDegraded = semantic.Degraded;
            await scopeResolver.AddMissingPathsAsync(
                semanticContexts.Select(x => x.WorkspaceId)
                    .Concat(semanticDocuments.Select(x => x.WorkspaceId))
                    .Distinct()
                    .Where(workspaceId => !workspacePaths.ContainsKey(workspaceId)),
                workspacePaths,
                cancellationToken);
        }

        var composed = RetrievalCandidateComposer.Compose(
            new RetrievalCandidateSources(
                lexicalContextCandidates.Values.ToArray(),
                lexicalDocumentCandidates.Values.ToArray(),
                semanticContexts,
                semanticDocuments),
            lexical,
            new RetrievalCompositionSettings(
                workspacePaths,
                query,
                options.Semantic,
                new CandidateSelection(CandidateSelectionKind.Search, true),
                SemanticUsed: semanticUsed,
                RetrievalDegraded: retrievalDegraded),
            hybridCandidateRanker,
            retrievalDeduplicator);
        var matches = new List<(double Score, ContextSearchMatch? Context, DocumentSearchMatch? Document)>();
        matches.AddRange(composed.Contexts.Select(candidate =>
            (candidate.Score,
                (ContextSearchMatch?)ToContextMatch(candidate, workspacePaths),
                (DocumentSearchMatch?)null)));
        matches.AddRange(composed.Documents.Select(candidate =>
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
            composed.Diagnostics);
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

    private static string ValidateAndNormalize(
        ContextSearchRequest request,
        RetrievalOptions options,
        out int limit)
    {
        ArgumentNullException.ThrowIfNull(request);
        var query = request.Query?.Trim() ?? string.Empty;
        if (query.Length == 0 || query.Length > 4_000 || request.Workspaces is { Count: > 20 })
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSearchQuery);
        }

        limit = request.Limit ?? options.Search.DefaultLimit;
        if (limit <= 0 || limit > options.Search.MaxLimit)
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

}
