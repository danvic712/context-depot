namespace ContextDepot.Application.Embeddings.Dtos;

public sealed record VectorCoverageSnapshot(
    int ContextTotal,
    int ContextIndexed,
    int DocumentChunkTotal,
    int DocumentChunkIndexed)
{
    public double ContextCoverage => CalculateCoverage(ContextTotal, ContextIndexed);

    public double DocumentCoverage => CalculateCoverage(DocumentChunkTotal, DocumentChunkIndexed);

    public bool IsComplete => ContextCoverage >= 1 && DocumentCoverage >= 1;

    private static double CalculateCoverage(int total, int indexed) =>
        total == 0 ? 1 : Math.Clamp(indexed / (double)total, 0, 1);
}
