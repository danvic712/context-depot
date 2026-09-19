using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Contexts.Dtos;

namespace ContextDepot.Application.Bootstrap;

internal sealed class BootstrapResultSelector(SimpleTokenEstimator tokenEstimator)
{
    public (IReadOnlyList<ContextModel> Contexts, IReadOnlyList<DocumentExcerptModel> Documents, int EstimatedTokens) Select(
        IReadOnlyList<ContextModel> rankedContexts,
        IReadOnlyList<DocumentExcerptModel> rankedDocuments,
        int maxTokens)
    {
        var selectedContexts = new List<ContextModel>();
        var selectedDocuments = new List<DocumentExcerptModel>();
        var usedTokens = 0;

        foreach (var context in rankedContexts)
        {
            var cost = tokenEstimator.Estimate(context.Title, context.Content, string.Join(' ', context.Tags));
            if (usedTokens + cost > maxTokens)
            {
                continue;
            }

            selectedContexts.Add(context);
            usedTokens += cost;
        }

        foreach (var document in rankedDocuments)
        {
            var cost = tokenEstimator.Estimate(document.Path, document.HeadingPath, document.Content);
            if (usedTokens + cost > maxTokens)
            {
                continue;
            }

            selectedDocuments.Add(document);
            usedTokens += cost;
        }

        return (selectedContexts, selectedDocuments, usedTokens);
    }
}
