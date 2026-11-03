namespace ContextDepot.Application.IndexRepair;

public sealed class IndexRepairOptions
{
    public int BatchSize { get; set; } = 32;

    public int MaxBatchesPerCycle { get; set; } = 4;
}
