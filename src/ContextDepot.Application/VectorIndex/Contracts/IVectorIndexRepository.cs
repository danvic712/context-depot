using ContextDepot.Application.VectorIndex.Dtos;

namespace ContextDepot.Application.VectorIndex.Contracts;

public interface IVectorIndexRepository
{
    Task<IReadOnlyDictionary<Guid, string>> GetContextInputHashesAsync(
        IReadOnlyList<Guid> contextItemIds,
        CancellationToken cancellationToken);

    Task<IReadOnlyDictionary<Guid, string>> GetDocumentInputHashesAsync(
        IReadOnlyList<Guid> documentChunkIds,
        CancellationToken cancellationToken);

    Task UpsertContextVectorsAsync(
        IReadOnlyList<ContextVectorIndexWrite> writes,
        CancellationToken cancellationToken);

    Task UpsertDocumentVectorsAsync(
        IReadOnlyList<DocumentVectorIndexWrite> writes,
        CancellationToken cancellationToken);

    Task DeleteContextVectorsAsync(
        IReadOnlyList<Guid> contextItemIds,
        CancellationToken cancellationToken);

    Task DeleteDocumentVectorsAsync(
        IReadOnlyList<Guid> documentChunkIds,
        CancellationToken cancellationToken);
}
