namespace ContextDepot.Application.IndexRepair.Dtos;

public sealed record IndexRepairCycleResult(
    int DocumentsReconciled,
    int ContextVectorsCreatedOrUpdated,
    int DocumentVectorsCreatedOrUpdated,
    Guid? NextContextAfterId,
    Guid? NextDocumentChunkAfterId,
    bool ContextScanWrapped,
    bool DocumentScanWrapped,
    bool RetrievalDegraded);
