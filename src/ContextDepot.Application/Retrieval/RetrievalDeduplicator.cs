using System.Text.RegularExpressions;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Retrieval;

public sealed partial class RetrievalDeduplicator
{
    public IReadOnlyList<RankedContextCandidate> DeduplicateContexts(
        IReadOnlyList<RankedContextCandidate> rankedCandidates,
        SemanticRetrievalOptions options)
    {
        ArgumentNullException.ThrowIfNull(rankedCandidates);
        ArgumentNullException.ThrowIfNull(options);

        var retained = new List<RankedContextCandidate>();
        var tokens = new Dictionary<Guid, IReadOnlySet<string>>();
        foreach (var candidate in OrderContexts(rankedCandidates))
        {
            if (retained.Any(existing => ShouldSuppressContext(existing, candidate, options, tokens)))
            {
                continue;
            }

            retained.Add(candidate);
        }

        return retained;
    }

    public IReadOnlyList<RankedDocumentCandidate> DeduplicateDocuments(
        IReadOnlyList<RankedDocumentCandidate> rankedCandidates,
        SemanticRetrievalOptions options)
    {
        ArgumentNullException.ThrowIfNull(rankedCandidates);
        ArgumentNullException.ThrowIfNull(options);

        var retained = new List<RankedDocumentCandidate>();
        var tokens = new Dictionary<Guid, IReadOnlySet<string>>();
        foreach (var candidate in OrderDocuments(rankedCandidates))
        {
            if (retained.Any(existing => ShouldSuppressDocument(existing, candidate, options, tokens)))
            {
                continue;
            }

            retained.Add(candidate);
        }

        return retained;
    }

    private bool ShouldSuppressContext(
        RankedContextCandidate retained,
        RankedContextCandidate candidate,
        SemanticRetrievalOptions options,
        Dictionary<Guid, IReadOnlySet<string>> tokens)
    {
        var retainedContext = retained.Context;
        var context = candidate.Context;
        if (retainedContext.WorkspaceId != context.WorkspaceId ||
            retainedContext.Kind != context.Kind ||
            retainedContext.Kind == ContextKind.Event ||
            !string.IsNullOrWhiteSpace(retainedContext.Key) ||
            !string.IsNullOrWhiteSpace(context.Key))
        {
            return false;
        }

        return MeetsDuplicateThresholds(
            GetTokens(tokens, retained.SourceIdentity, () => BuildContextText(retainedContext)),
            GetTokens(tokens, candidate.SourceIdentity, () => BuildContextText(context)),
            options);
    }

    private bool ShouldSuppressDocument(
        RankedDocumentCandidate retained,
        RankedDocumentCandidate candidate,
        SemanticRetrievalOptions options,
        Dictionary<Guid, IReadOnlySet<string>> tokens)
    {
        var retainedDocument = retained.Document;
        var document = candidate.Document;
        if (retainedDocument.DocumentId != document.DocumentId)
        {
            return false;
        }

        return MeetsDuplicateThresholds(
            GetTokens(tokens, retained.SourceIdentity, () => BuildDocumentText(retainedDocument)),
            GetTokens(tokens, candidate.SourceIdentity, () => BuildDocumentText(document)),
            options);
    }

    private static bool MeetsDuplicateThresholds(
        IReadOnlySet<string> firstTokens,
        IReadOnlySet<string> secondTokens,
        SemanticRetrievalOptions options)
    {
        if (firstTokens.Count == 0 || secondTokens.Count == 0)
        {
            return false;
        }

        var intersection = firstTokens.Count(secondTokens.Contains);
        var union = firstTokens.Count + secondTokens.Count - intersection;
        var shorter = Math.Min(firstTokens.Count, secondTokens.Count);
        if (union == 0 || shorter == 0)
        {
            return false;
        }

        var semanticSimilarity = intersection / (double)union;
        var tokenOverlap = intersection / (double)shorter;
        return semanticSimilarity >= options.DedupSimilarityThreshold &&
               tokenOverlap >= options.DedupTokenOverlapThreshold;
    }

    private static string BuildContextText(BootstrapContextCandidate context) =>
        $"{context.Title} {context.Content} {context.TagsJson}";

    private static IReadOnlySet<string> GetTokens(Dictionary<Guid, IReadOnlySet<string>> cache, Guid id, Func<string> text)
    {
        if (cache.TryGetValue(id, out var tokens)) return tokens;
        tokens = Tokenize(text());
        cache[id] = tokens;
        return tokens;
    }

    private static string BuildDocumentText(BootstrapDocumentChunkCandidate document) =>
        $"{document.Title} {document.HeadingPath} {document.Content}";

    private static IReadOnlyList<RankedContextCandidate> OrderContexts(
        IReadOnlyList<RankedContextCandidate> candidates) =>
        candidates
            .OrderByDescending(candidate => candidate.IsExactMatch)
            .ThenByDescending(candidate => candidate.ExactScore)
            .ThenByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Context.Importance)
            .ThenByDescending(candidate => candidate.Context.UpdatedAt)
            .ThenBy(candidate => candidate.SourceIdentity)
            .ToArray();

    private static IReadOnlyList<RankedDocumentCandidate> OrderDocuments(
        IReadOnlyList<RankedDocumentCandidate> candidates) =>
        candidates
            .OrderByDescending(candidate => candidate.IsExactMatch)
            .ThenByDescending(candidate => candidate.ExactScore)
            .ThenByDescending(candidate => candidate.Score)
            .ThenBy(candidate => candidate.Document.Ordinal)
            .ThenBy(candidate => candidate.SourceIdentity)
            .ToArray();

    private static IReadOnlySet<string> Tokenize(string value)
    {
        var normalized = value.ToLowerInvariant();
        var tokens = new HashSet<string>(StringComparer.Ordinal);
        foreach (var match in TokenRegex().EnumerateMatches(normalized))
        {
            tokens.Add(normalized.AsSpan(match.Index, match.Length).ToString());
        }

        return tokens;
    }

    [GeneratedRegex("[\\p{L}\\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
