namespace ContextDepot.Application.Embeddings;

public sealed class SemanticRetrievalOptions
{
    public int ScopeTopK { get; set; } = 8;

    public int CandidateTopKPerSource { get; set; } = 20;

    public int OversampleFactor { get; set; } = 3;

    public double RetrievalLexicalFallbackThreshold { get; set; } = 0.65;

    public double DedupSimilarityThreshold { get; set; } = 0.98;

    public double DedupTokenOverlapThreshold { get; set; } = 0.80;
}
