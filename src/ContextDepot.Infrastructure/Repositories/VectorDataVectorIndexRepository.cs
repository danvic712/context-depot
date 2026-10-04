using ContextDepot.Infrastructure.Exceptions;
using ContextDepot.Application.Embeddings;
using ContextDepot.Application.VectorIndex.Contracts;
using ContextDepot.Application.VectorIndex.Dtos;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Infrastructure.VectorStore;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.VectorData;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class VectorDataVectorIndexRepository : IVectorIndexRepository
{
    private readonly VectorStoreCollection<Guid, ContextVectorRecord>? _contextCollection;
    private readonly VectorStoreCollection<Guid, DocumentVectorRecord>? _documentCollection;

    private VectorStoreCollection<Guid, ContextVectorRecord> ContextCollection =>
        _contextCollection ?? throw new InvalidOperationException(InfrastructureErrorCodes.EmbeddingRouteRequired);
    private VectorStoreCollection<Guid, DocumentVectorRecord> DocumentCollection =>
        _documentCollection ?? throw new InvalidOperationException(InfrastructureErrorCodes.EmbeddingRouteRequired);

    public VectorDataVectorIndexRepository(
        PostgreSqlVectorStore vectorStore,
        ScopedInferenceRuntimeSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(vectorStore);
        ArgumentNullException.ThrowIfNull(snapshot);
        var embedding = snapshot.Value.Embedding;
        if (embedding is null)
        {
            return;
        }
        _contextCollection = vectorStore.GetCollection<Guid, ContextVectorRecord>(
            VectorCollectionNamePolicy.CreateContextCollectionName(embedding.ProfileFingerprint),
            VectorCollectionDefinitions.CreateContext(embedding.Dimensions));
        _documentCollection = vectorStore.GetCollection<Guid, DocumentVectorRecord>(
            VectorCollectionNamePolicy.CreateDocumentCollectionName(embedding.ProfileFingerprint),
            VectorCollectionDefinitions.CreateDocument(embedding.Dimensions));
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetContextInputHashesAsync(
        IReadOnlyList<Guid> contextItemIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextItemIds);
        var records = await ReadContextRecordsAsync(contextItemIds, cancellationToken);
        return records.ToDictionary(record => record.ContextItemId, record => record.EmbeddingInputHash);
    }

    public async Task<IReadOnlyDictionary<Guid, string>> GetDocumentInputHashesAsync(
        IReadOnlyList<Guid> documentChunkIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentChunkIds);
        var records = await ReadDocumentRecordsAsync(documentChunkIds, cancellationToken);
        return records.ToDictionary(record => record.DocumentChunkId, record => record.EmbeddingInputHash);
    }

    public Task UpsertContextVectorsAsync(
        IReadOnlyList<ContextVectorIndexWrite> writes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writes);
        return ContextCollection.UpsertAsync(
            writes.Select(write => new ContextVectorRecord
            {
                ContextItemId = write.ContextItemId,
                DepotId = write.DepotId,
                WorkspaceId = write.WorkspaceId,
                Kind = write.Kind.ToString().ToLowerInvariant(),
                EmbeddingInputHash = write.EmbeddingInputHash,
                Embedding = write.Embedding
            }),
            cancellationToken);
    }

    public Task UpsertDocumentVectorsAsync(
        IReadOnlyList<DocumentVectorIndexWrite> writes,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(writes);
        return DocumentCollection.UpsertAsync(
            writes.Select(write => new DocumentVectorRecord
            {
                DocumentChunkId = write.DocumentChunkId,
                DocumentId = write.DocumentId,
                DepotId = write.DepotId,
                WorkspaceId = write.WorkspaceId,
                EmbeddingInputHash = write.EmbeddingInputHash,
                Embedding = write.Embedding
            }),
            cancellationToken);
    }

    public Task DeleteContextVectorsAsync(
        IReadOnlyList<Guid> contextItemIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(contextItemIds);
        return ContextCollection.DeleteAsync(contextItemIds, cancellationToken);
    }

    public Task DeleteDocumentVectorsAsync(
        IReadOnlyList<Guid> documentChunkIds,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(documentChunkIds);
        return DocumentCollection.DeleteAsync(documentChunkIds, cancellationToken);
    }

    private async Task<IReadOnlyList<ContextVectorRecord>> ReadContextRecordsAsync(
        IReadOnlyList<Guid> keys,
        CancellationToken cancellationToken)
    {
        var records = new List<ContextVectorRecord>();
        await foreach (var record in ContextCollection.GetAsync(keys, cancellationToken: cancellationToken))
        {
            records.Add(record);
        }

        return records;
    }

    private async Task<IReadOnlyList<DocumentVectorRecord>> ReadDocumentRecordsAsync(
        IReadOnlyList<Guid> keys,
        CancellationToken cancellationToken)
    {
        var records = new List<DocumentVectorRecord>();
        await foreach (var record in DocumentCollection.GetAsync(keys, cancellationToken: cancellationToken))
        {
            records.Add(record);
        }

        return records;
    }
}
