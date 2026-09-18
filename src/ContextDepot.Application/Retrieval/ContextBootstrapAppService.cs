using System.Text.Json;
using System.Text.RegularExpressions;
using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Contexts;
using ContextDepot.Application.Persistence;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Retrieval;

public sealed partial class ContextBootstrapAppService(
    ICurrentOwnerContext currentOwner,
    IWorkspaceAppService workspaceAppService,
    IBootstrapRepository repository) : IContextBootstrapAppService
{
    public async Task<BootstrapResult> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken)
    {
        var maxTokens = Math.Clamp(request.MaxTokens, 1, 8_000);
        var query = request.Query?.Trim() ?? string.Empty;
        var queryTokens = Tokens(query);
        var workspaces = await repository.GetWorkspacesAsync(currentOwner.OwnerId, cancellationToken);
        var workspaceById = workspaces.ToDictionary(x => x.Id);
        var contexts = await repository.GetActiveContextsAsync(currentOwner.OwnerId, cancellationToken);
        var chunks = await repository.GetIndexedDocumentChunksAsync(currentOwner.OwnerId, cancellationToken);

        HashSet<Guid>? scopeIds = null;
        ScopeResolution scopeResolution;
        if (request.Workspaces is { Count: > 0 })
        {
            scopeIds = [];
            var resolvedPaths = new List<string>();
            foreach (var path in request.Workspaces)
            {
                var workspace = await workspaceAppService.ResolveAsync(path, cancellationToken)
                    ?? throw new ContextDepotApplicationException("WorkspaceNotFound", "One of the requested workspaces does not exist.");
                scopeIds.Add(workspace.Id);
                resolvedPaths.Add(workspace.Path);
            }

            scopeResolution = new ScopeResolution(ScopeResolutionStatus.Resolved, resolvedPaths);
        }
        else
        {
            (scopeResolution, scopeIds) = ResolveAutoScope(queryTokens, workspaces, contexts, chunks);
        }

        var rankedContexts = contexts
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .Select(x => (Context: x, Score: ScoreContext(x, query, queryTokens)))
            .Where(x => x.Score > 0 || queryTokens.Count == 0)
            .OrderByDescending(x => x.Score)
            .ThenByDescending(x => x.Context.Importance)
            .ThenByDescending(x => x.Context.UpdatedAt)
            .Select(x => ToModel(x.Context))
            .ToArray();

        var rankedDocuments = chunks
            .Where(x => scopeIds is null || scopeIds.Contains(x.WorkspaceId))
            .Select(x => (Chunk: x, Score: ScoreChunk(x, query, queryTokens)))
            .Where(x => x.Score > 0 || queryTokens.Count == 0)
            .OrderByDescending(x => x.Score)
            .ThenBy(x => x.Chunk.Ordinal)
            .Select(x => ToExcerpt(x.Chunk, workspaceById))
            .ToArray();

        var selectedContexts = new List<ContextModel>();
        var selectedDocuments = new List<DocumentExcerptModel>();
        var usedTokens = 0;
        foreach (var context in rankedContexts)
        {
            var cost = EstimateTokens(context.Title, context.Content);
            if (usedTokens + cost > maxTokens)
            {
                continue;
            }

            selectedContexts.Add(context);
            usedTokens += cost;
        }

        foreach (var document in rankedDocuments)
        {
            var cost = EstimateTokens(document.HeadingPath, document.Content);
            if (usedTokens + cost > maxTokens)
            {
                continue;
            }

            selectedDocuments.Add(document);
            usedTokens += cost;
        }

        return new BootstrapResult(scopeResolution, selectedContexts, selectedDocuments, new RetrievalDiagnostics(false, false, "lexical"), usedTokens);
    }

    private static (ScopeResolution Resolution, HashSet<Guid>? ScopeIds) ResolveAutoScope(
        IReadOnlySet<string> queryTokens,
        IReadOnlyList<Workspace> workspaces,
        IReadOnlyList<ContextItem> contexts,
        IReadOnlyList<DocumentChunk> chunks)
    {
        if (queryTokens.Count == 0)
        {
            return (new ScopeResolution(ScopeResolutionStatus.Broad, []), null);
        }

        var scores = new List<(Workspace Workspace, double Score)>();
        foreach (var workspace in workspaces)
        {
            var pathTokens = Tokens(workspace.Slug);
            var pathScore = queryTokens.Count(token => pathTokens.Contains(token));
            var contextScore = contexts.Where(x => x.WorkspaceId == workspace.Id).Select(x => ScoreContext(x, string.Empty, queryTokens)).DefaultIfEmpty(0).Max();
            var documentScore = chunks.Where(x => x.WorkspaceId == workspace.Id).Select(x => ScoreChunk(x, string.Empty, queryTokens)).DefaultIfEmpty(0).Max();
            var score = pathScore * 5 + contextScore + documentScore;
            if (score > 0)
            {
                scores.Add((workspace, score));
            }
        }

        if (scores.Count == 0)
        {
            return (new ScopeResolution(ScopeResolutionStatus.Broad, []), null);
        }

        var ordered = scores.OrderByDescending(x => x.Score).ToArray();
        var best = ordered[0];
        var close = ordered.Skip(1).Where(x => x.Score >= best.Score * 0.85).Select(x => x.Workspace).ToArray();
        if (close.Length > 0)
        {
            return (new ScopeResolution(ScopeResolutionStatus.Ambiguous, new[] { best.Workspace }.Concat(close).Select(x => x.Slug).ToArray()), null);
        }

        return (new ScopeResolution(ScopeResolutionStatus.Resolved, [best.Workspace.Slug]), [best.Workspace.Id]);
    }

    private static double ScoreContext(ContextItem context, string query, IReadOnlySet<string> tokens)
    {
        var score = 0d;
        var contentTokens = Tokens($"{context.Title} {context.Content} {context.TagsJson}");
        score += tokens.Count(token => contentTokens.Contains(token));
        if (!string.IsNullOrWhiteSpace(context.Key) && query.Contains(context.Key, StringComparison.OrdinalIgnoreCase))
        {
            score += 100;
        }

        if (!string.IsNullOrWhiteSpace(context.Title) && query.Contains(context.Title, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        return score + context.Importance / 100d;
    }

    private static double ScoreChunk(DocumentChunk chunk, string query, IReadOnlySet<string> tokens)
    {
        var score = tokens.Count(token => Tokens($"{chunk.HeadingPath} {chunk.Content}").Contains(token));
        if (!string.IsNullOrWhiteSpace(chunk.HeadingPath) && query.Contains(chunk.HeadingPath, StringComparison.OrdinalIgnoreCase))
        {
            score += 25;
        }

        return score;
    }

    private static IReadOnlySet<string> Tokens(string value) => TokenRegex().Matches(value.ToLowerInvariant()).Select(x => x.Value).ToHashSet(StringComparer.Ordinal);

    private static int EstimateTokens(params string?[] values) => Math.Max(1, Tokens(string.Join(' ', values)).Count);

    private static ContextModel ToModel(ContextItem context)
    {
        var tags = JsonSerializer.Deserialize<string[]>(context.TagsJson) ?? [];
        return new ContextModel(context.Id, context.OwnerId, context.WorkspaceId, context.Kind, context.Key, context.Title, context.Content, tags, context.Importance, context.Confidence, context.Status, context.VerificationStatus, context.ProvenanceTrust, context.SourceType, context.SourceAgent, context.SourceRef, context.SupersedesId, context.ExpiresAt, context.CreatedAt, context.UpdatedAt);
    }

    private static DocumentExcerptModel ToExcerpt(DocumentChunk chunk, IReadOnlyDictionary<Guid, Workspace> workspaces)
    {
        var workspacePath = workspaces.TryGetValue(chunk.WorkspaceId, out var workspace) ? workspace.Slug : string.Empty;
        var path = chunk.Document is null ? string.Empty : (workspacePath.Length == 0 ? chunk.Document.Path : workspacePath + "/" + chunk.Document.Path);
        return new DocumentExcerptModel(chunk.DocumentId, chunk.Id, path, chunk.HeadingPath, chunk.Content, chunk.ContentHash);
    }

    [GeneratedRegex("[a-z0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
