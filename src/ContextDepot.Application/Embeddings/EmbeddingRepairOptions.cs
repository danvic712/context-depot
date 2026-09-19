namespace ContextDepot.Application.Embeddings;

public sealed class EmbeddingRepairOptions
{
    public int PollIntervalSeconds { get; set; } = 30;

    public int BatchSize { get; set; } = 32;

    public int MaxBatchesPerCycle { get; set; } = 4;
}
