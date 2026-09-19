using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.Embeddings;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.SemanticRetrieval;

public sealed class SemanticFallbackDecider(IOptions<RetrievalOptions> options)
{
    private readonly SemanticRetrievalOptions semantic = options.Value.Semantic;

    public int ScopeTopK => semantic.ScopeTopK;

    public int CandidateTopKPerSource => semantic.CandidateTopKPerSource;

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
        double topLexicalScore)
    {
        if (!hasQuerySignal)
        {
            return false;
        }

        if (!hasLexicalCandidates)
        {
            return true;
        }

        var normalizedScore = Math.Clamp(topLexicalScore, 0, 1);
        return normalizedScore < semantic.RetrievalLexicalFallbackThreshold;
    }
}
