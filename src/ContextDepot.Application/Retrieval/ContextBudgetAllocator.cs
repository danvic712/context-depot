using ContextDepot.Application.Bootstrap;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Contexts.Dtos;

namespace ContextDepot.Application.Retrieval;

public sealed class ContextBudgetAllocator
{
    private readonly SimpleTokenEstimator tokenEstimator = new();

    public (IReadOnlyList<ContextModel> Contexts, IReadOnlyList<DocumentExcerptModel> Documents, int EstimatedTokens) Allocate(
        IReadOnlyList<ContextModel> contexts,
        IReadOnlyList<DocumentExcerptModel> documents,
        int maxTokens)
        => Allocate(contexts, documents, maxTokens, null, null);

    public (IReadOnlyList<ContextModel> Contexts, IReadOnlyList<DocumentExcerptModel> Documents, int EstimatedTokens) Allocate(
        IReadOnlyList<ContextModel> contexts,
        IReadOnlyList<DocumentExcerptModel> documents,
        int maxTokens,
        IReadOnlyDictionary<Guid, double>? contextScores,
        IReadOnlyDictionary<Guid, double>? documentScores)
    {
        ArgumentNullException.ThrowIfNull(contexts);
        ArgumentNullException.ThrowIfNull(documents);
        if (maxTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(maxTokens));
        }

        var selectedContexts = new List<ContextModel>();
        var selectedDocuments = new List<DocumentExcerptModel>();
        var usedTokens = 0;

        var candidates = new List<(
            bool IsContext,
            int Index,
            double Score,
            ContextModel? Context,
            DocumentExcerptModel? Document)>();
        candidates.AddRange(contexts.Select((context, index) =>
            (IsContext: true,
                Index: index,
                Score: contextScores?.GetValueOrDefault(context.Id) ?? 0,
                Context: (ContextModel?)context,
                Document: (DocumentExcerptModel?)null)));
        candidates.AddRange(documents.Select((document, index) =>
            (IsContext: false,
                Index: index,
                Score: documentScores?.GetValueOrDefault(document.ChunkId) ?? 0,
                Context: (ContextModel?)null,
                Document: (DocumentExcerptModel?)document)));
        var orderedCandidates = candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.IsContext)
            .ThenBy(candidate => candidate.Index);

        foreach (var candidate in orderedCandidates)
        {
            var cost = candidate.IsContext
                ? tokenEstimator.Estimate(
                    candidate.Context!.Title,
                    candidate.Context.Content,
                    string.Join(' ', candidate.Context.Tags))
                : tokenEstimator.Estimate(
                    candidate.Document!.Path,
                    candidate.Document.HeadingPath,
                    candidate.Document.Content);
            if (usedTokens + cost > maxTokens)
            {
                continue;
            }

            if (candidate.IsContext)
            {
                selectedContexts.Add(candidate.Context!);
            }
            else
            {
                selectedDocuments.Add(candidate.Document!);
            }

            usedTokens += cost;
        }

        return (selectedContexts, selectedDocuments, usedTokens);
    }

    public (IReadOnlyList<ContextModel> Contexts, IReadOnlyList<DocumentExcerptModel> Documents, int EstimatedTokens) Select(
        IReadOnlyList<ContextModel> contexts,
        IReadOnlyList<DocumentExcerptModel> documents,
        int maxTokens) =>
        Allocate(contexts, documents, maxTokens);

    public (IReadOnlyList<ContextModel> Contexts, IReadOnlyList<DocumentExcerptModel> Documents, int EstimatedTokens) Select(
        IReadOnlyList<ContextModel> contexts,
        IReadOnlyList<DocumentExcerptModel> documents,
        int maxTokens,
        IReadOnlyDictionary<Guid, double>? contextScores,
        IReadOnlyDictionary<Guid, double>? documentScores) =>
        Allocate(contexts, documents, maxTokens, contextScores, documentScores);
}
