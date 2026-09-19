namespace ContextDepot.Application.IndexRepair.Dtos;

public sealed record IndexRepairRequest(
    int BatchSize,
    int MaxBatches,
    Guid? ContextAfterId = null,
    Guid? DocumentChunkAfterId = null);
