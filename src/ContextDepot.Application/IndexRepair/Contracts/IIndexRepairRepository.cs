using ContextDepot.Application.IndexRepair.Dtos;

namespace ContextDepot.Application.IndexRepair.Contracts;

public interface IIndexRepairRepository
{
    Task<IReadOnlyList<DocumentIndexRepairCandidate>> FindDocumentIndexRepairCandidatesAsync(
        Guid depotId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContextEmbeddingRepairCandidate>> FindContextSourcePageAsync(
        Guid depotId,
        Guid? afterContextId,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentEmbeddingRepairCandidate>> FindDocumentChunkSourcePageAsync(
        Guid depotId,
        Guid? afterDocumentChunkId,
        int limit,
        CancellationToken cancellationToken);

    Task MarkDocumentIndexFailedAsync(
        Guid depotId,
        Guid documentId,
        string errorCode,
        CancellationToken cancellationToken);
}
