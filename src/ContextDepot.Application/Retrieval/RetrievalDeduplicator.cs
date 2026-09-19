using System.Text.RegularExpressions;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Domain.Contexts.Enums;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.Retrieval;

public sealed partial class RetrievalDeduplicator(IOptions<RetrievalOptions> options)
{
    private readonly double similarityThreshold = options.Value.Semantic.DedupSimilarityThreshold;
    private readonly double tokenOverlapThreshold = options.Value.Semantic.DedupTokenOverlapThreshold;

    public IReadOnlyList<RankedContextCandidate> DeduplicateContexts(
        IReadOnlyList<RankedContextCandidate> rankedCandidates)
    {
        ArgumentNullException.ThrowIfNull(rankedCandidates);

        var retained = new List<RankedContextCandidate>();
        foreach (var candidate in OrderContexts(rankedCandidates))
        {
            if (retained.Any(existing => ShouldSuppressContext(existing, candidate)))
            {
                continue;
            }

            retained.Add(candidate);
        }

        return retained;
    }

    public IReadOnlyList<RankedDocumentCandidate> DeduplicateDocuments(
        IReadOnlyList<RankedDocumentCandidate> rankedCandidates)
    {
        ArgumentNullException.ThrowIfNull(rankedCandidates);

        var retained = new List<RankedDocumentCandidate>();
        foreach (var candidate in OrderDocuments(rankedCandidates))
        {
            if (retained.Any(existing => ShouldSuppressDocument(existing, candidate)))
            {
                continue;
            }

            retained.Add(candidate);
        }

        return retained;
    }

    private bool ShouldSuppressContext(
        RankedContextCandidate retained,
        RankedContextCandidate candidate)
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
            BuildContextText(retainedContext),
            BuildContextText(context));
    }

    private bool ShouldSuppressDocument(
        RankedDocumentCandidate retained,
        RankedDocumentCandidate candidate)
    {
        var retainedDocument = retained.Document;
        var document = candidate.Document;
        if (retainedDocument.DocumentId != document.DocumentId)
        {
            return false;
        }

        return MeetsDuplicateThresholds(
            BuildDocumentText(retainedDocument),
            BuildDocumentText(document));
    }

    private bool MeetsDuplicateThresholds(string first, string second)
    {
        var firstTokens = Tokenize(first);
        var secondTokens = Tokenize(second);
        if (firstTokens.Count == 0 || secondTokens.Count == 0)
        {
            return false;
        }

        var intersection = firstTokens.Intersect(secondTokens, StringComparer.Ordinal).Count();
        var union = firstTokens.Union(secondTokens, StringComparer.Ordinal).Count();
        var shorter = Math.Min(firstTokens.Count, secondTokens.Count);
        if (union == 0 || shorter == 0)
        {
            return false;
        }

        var semanticSimilarity = intersection / (double)union;
        var tokenOverlap = intersection / (double)shorter;
        return semanticSimilarity >= similarityThreshold && tokenOverlap >= tokenOverlapThreshold;
    }

    private static string BuildContextText(BootstrapContextCandidate context) =>
        $"{context.Title} {context.Content} {context.TagsJson}";

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

    private static IReadOnlySet<string> Tokenize(string value) =>
        TokenRegex().Matches(value.ToLowerInvariant())
            .Select(match => match.Value)
            .ToHashSet(StringComparer.Ordinal);

    [GeneratedRegex("[\\p{L}\\p{N}]+", RegexOptions.CultureInvariant)]
    private static partial Regex TokenRegex();
}
