using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Application.SemanticRetrieval.Dtos;

namespace ContextDepot.Application.Retrieval;

public static class RetrievalCandidateComposer
{
    public static LexicalEvidence AnalyzeLexical(
        IReadOnlyCollection<BootstrapContextCandidate> contexts,
        IReadOnlyCollection<BootstrapDocumentChunkCandidate> documents,
        IReadOnlyDictionary<Guid, string> workspacePaths,
        string query,
        IReadOnlySet<string> queryTokens,
        IReadOnlySet<Guid>? scopeIds = null)
    {
        var ranker = new ScopeCandidateRanker();
        var contextScores = contexts
            .Where(candidate => scopeIds is null || scopeIds.Contains(candidate.WorkspaceId))
            .ToDictionary(candidate => candidate.Id, candidate => ranker.ScoreContext(
                candidate, workspacePaths.GetValueOrDefault(candidate.WorkspaceId, string.Empty), query, queryTokens));
        var documentScores = documents
            .Where(candidate => scopeIds is null || scopeIds.Contains(candidate.WorkspaceId))
            .ToDictionary(candidate => candidate.Id, candidate => ranker.ScoreDocument(
                candidate, workspacePaths.GetValueOrDefault(candidate.WorkspaceId, string.Empty), query, queryTokens));
        var signals = contexts
            .Where(candidate => contextScores.ContainsKey(candidate.Id))
            .Select(candidate => contextScores[candidate.Id] - candidate.Importance / 100d)
            .Concat(documentScores.Values)
            .ToArray();
        return new LexicalEvidence(
            contextScores,
            documentScores,
            signals.Any(score => score > 0),
            signals.Select(score => Math.Clamp(score / 10d, 0, 1)).DefaultIfEmpty(0).Max());
    }

    public static ComposedCandidates Compose(
        RetrievalCandidateSources sources,
        LexicalEvidence lexical,
        RetrievalCompositionSettings settings,
        HybridCandidateRanker ranker,
        RetrievalDeduplicator deduplicator)
    {
        var contexts = sources.LexicalContexts.ToDictionary(candidate => candidate.Id);
        foreach (var candidate in sources.SemanticContexts)
        {
            contexts[candidate.ContextItemId] = candidate.Context;
        }

        var documents = sources.LexicalDocuments.ToDictionary(candidate => candidate.Id);
        foreach (var candidate in sources.SemanticDocuments)
        {
            documents[candidate.DocumentChunkId] = candidate.Document;
        }

        var contextScores = sources.SemanticContexts.GroupBy(candidate => candidate.ContextItemId)
            .ToDictionary(group => group.Key, group => group.Max(candidate => candidate.Similarity));
        var documentScores = sources.SemanticDocuments.GroupBy(candidate => candidate.DocumentChunkId)
            .ToDictionary(group => group.Key, group => group.Max(candidate => candidate.Similarity));

        var rankedContexts = ranker.RankContexts(
                contexts.Values.Where(candidate => settings.ScopeIds is null || settings.ScopeIds.Contains(candidate.WorkspaceId)).ToArray(),
                settings.Query,
                settings.WorkspacePaths,
                contextScores,
                lexical.ContextScores)
            .Where(candidate => AcceptContext(candidate, lexical.ContextScores, settings.Selection))
            .ToArray();
        var rankedDocuments = ranker.RankDocuments(
                documents.Values.Where(candidate => settings.ScopeIds is null || settings.ScopeIds.Contains(candidate.WorkspaceId)).ToArray(),
                settings.Query,
                settings.WorkspacePaths,
                documentScores,
                lexical.DocumentScores)
            .Where(candidate => AcceptDocument(candidate, lexical.DocumentScores, settings.Selection))
            .ToArray();

        return new ComposedCandidates(
            deduplicator.DeduplicateContexts(rankedContexts, settings.Options),
            deduplicator.DeduplicateDocuments(rankedDocuments, settings.Options),
            new RetrievalDiagnostics(
                settings.RetrievalDegraded,
                settings.SemanticUsed,
                settings.SemanticUsed ? "hybrid" : settings.RetrievalDegraded ? "lexical-degraded" : "lexical"));
    }

    private static bool AcceptContext(RankedContextCandidate candidate, IReadOnlyDictionary<Guid, double> scores, CandidateSelection selection)
    {
        if (candidate.SemanticScore >= 0.65)
        {
            return true;
        }

        var lexicalSignal = scores.GetValueOrDefault(candidate.Context.Id) - candidate.Context.Importance / 100d;
        return selection.Kind switch
        {
            CandidateSelectionKind.Search => lexicalSignal > 0,
            CandidateSelectionKind.BootstrapBroad => lexicalSignal >= 2,
            _ => selection.HasQueryTokens == false || lexicalSignal > 0
        };
    }

    private static bool AcceptDocument(RankedDocumentCandidate candidate, IReadOnlyDictionary<Guid, double> scores, CandidateSelection selection)
    {
        if (candidate.SemanticScore >= 0.65)
        {
            return true;
        }

        return selection.Kind switch
        {
            CandidateSelectionKind.Search => scores.GetValueOrDefault(candidate.Document.Id) > 0,
            CandidateSelectionKind.BootstrapBroad => scores.GetValueOrDefault(candidate.Document.Id) >= 2,
            _ => candidate.LexicalScore > 0 || selection.HasQueryTokens == false
        };
    }
}

public sealed record ComposedCandidates(
    IReadOnlyList<RankedContextCandidate> Contexts,
    IReadOnlyList<RankedDocumentCandidate> Documents,
    RetrievalDiagnostics Diagnostics);

public sealed record RetrievalCandidateSources(
    IReadOnlyCollection<BootstrapContextCandidate> LexicalContexts,
    IReadOnlyCollection<BootstrapDocumentChunkCandidate> LexicalDocuments,
    IReadOnlyCollection<SemanticContextCandidateRecord> SemanticContexts,
    IReadOnlyCollection<SemanticDocumentCandidateRecord> SemanticDocuments);

public sealed record RetrievalCompositionSettings(
    IReadOnlyDictionary<Guid, string> WorkspacePaths,
    string Query,
    SemanticRetrievalOptions Options,
    CandidateSelection Selection,
    IReadOnlySet<Guid>? ScopeIds = null,
    bool SemanticUsed = false,
    bool RetrievalDegraded = false);

public sealed record LexicalEvidence(
    IReadOnlyDictionary<Guid, double> ContextScores,
    IReadOnlyDictionary<Guid, double> DocumentScores,
    bool HasCandidates,
    double TopScore);

public sealed record CandidateSelection(CandidateSelectionKind Kind, bool HasQueryTokens);

public enum CandidateSelectionKind { Search, Bootstrap, BootstrapBroad }
