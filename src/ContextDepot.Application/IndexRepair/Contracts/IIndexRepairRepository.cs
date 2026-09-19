using ContextDepot.Application.IndexRepair.Dtos;

namespace ContextDepot.Application.IndexRepair.Contracts;

public interface IIndexRepairRepository
{
    Task<IReadOnlyList<DocumentIndexRepairCandidate>> FindDocumentIndexRepairCandidatesAsync(
        Guid ownerId,
        int limit,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContextEmbeddingRepairCandidate>> FindContextSourcePageAsync(
        Guid ownerId,
        Guid? afterContextId,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentEmbeddingRepairCandidate>> FindDocumentChunkSourcePageAsync(
        Guid ownerId,
        Guid? afterDocumentChunkId,
        int limit,
        CancellationToken cancellationToken);

    Task MarkDocumentIndexFailedAsync(
        Guid ownerId,
        Guid documentId,
        string errorCode,
        CancellationToken cancellationToken);
}
