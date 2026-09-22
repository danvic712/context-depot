using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.Embeddings;

namespace ContextDepot.Application.SemanticRetrieval;

public sealed class SemanticFallbackDecider
{
    public bool ShouldUseForScope(
        string normalizedQuery,
        bool hasExplicitScope,
        ScopeResolution resolution) =>
        !hasExplicitScope &&
        !string.IsNullOrWhiteSpace(normalizedQuery) &&
        resolution.Status != ScopeResolutionStatus.Resolved;

    public bool ShouldUseForRetrieval(
        bool hasQuerySignal,
        bool hasLexicalCandidates,
        double topLexicalScore,
        SemanticRetrievalOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (!hasQuerySignal)
        {
            return false;
        }

        if (!hasLexicalCandidates)
        {
            return true;
        }

        var normalizedScore = Math.Clamp(topLexicalScore, 0, 1);
        return normalizedScore < options.RetrievalLexicalFallbackThreshold;
    }
}
