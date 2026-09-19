using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Retrieval;

namespace ContextDepot.Application.Bootstrap;

internal sealed class BootstrapResultSelector(ContextBudgetAllocator budgetAllocator)
{
    public (IReadOnlyList<ContextModel> Contexts, IReadOnlyList<DocumentExcerptModel> Documents, int EstimatedTokens) Select(
        IReadOnlyList<ContextModel> rankedContexts,
        IReadOnlyList<DocumentExcerptModel> rankedDocuments,
        int maxTokens,
        IReadOnlyDictionary<Guid, double>? contextScores = null,
        IReadOnlyDictionary<Guid, double>? documentScores = null)
        => budgetAllocator.Allocate(
            rankedContexts,
            rankedDocuments,
            maxTokens,
            contextScores,
            documentScores);
}
