using Microsoft.Extensions.VectorData;

namespace ContextDepot.Infrastructure.VectorStore;

public static class VectorCollectionDefinitions
{
    public static VectorStoreCollectionDefinition CreateContext(int dimensions)
    {
        ValidateDimensions(dimensions);

        return new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(ContextVectorRecord.ContextItemId), typeof(Guid))
                {
                    StorageName = "context_item_id"
                },
                new VectorStoreDataProperty(nameof(ContextVectorRecord.DepotId), typeof(Guid))
                {
                    StorageName = "depot_id",
                    IsIndexed = true
                },
                new VectorStoreDataProperty(nameof(ContextVectorRecord.WorkspaceId), typeof(Guid))
                {
                    StorageName = "workspace_id",
                    IsIndexed = true
                },
                new VectorStoreDataProperty(nameof(ContextVectorRecord.Kind), typeof(string))
                {
                    StorageName = "kind",
                    IsIndexed = true
                },
                new VectorStoreDataProperty(nameof(ContextVectorRecord.EmbeddingInputHash), typeof(string))
                {
                    StorageName = "embedding_input_hash"
                },
                new VectorStoreVectorProperty(nameof(ContextVectorRecord.Embedding), typeof(ReadOnlyMemory<float>), dimensions)
                {
                    StorageName = "embedding",
                    IndexKind = IndexKind.Flat,
                    DistanceFunction = DistanceFunction.CosineDistance
                }
            ]
        };
    }

    public static VectorStoreCollectionDefinition CreateDocument(int dimensions)
    {
        ValidateDimensions(dimensions);

        return new VectorStoreCollectionDefinition
        {
            Properties =
            [
                new VectorStoreKeyProperty(nameof(DocumentVectorRecord.DocumentChunkId), typeof(Guid))
                {
                    StorageName = "document_chunk_id"
                },
                new VectorStoreDataProperty(nameof(DocumentVectorRecord.DocumentId), typeof(Guid))
                {
                    StorageName = "document_id",
                    IsIndexed = true
                },
                new VectorStoreDataProperty(nameof(DocumentVectorRecord.DepotId), typeof(Guid))
                {
                    StorageName = "depot_id",
                    IsIndexed = true
                },
                new VectorStoreDataProperty(nameof(DocumentVectorRecord.WorkspaceId), typeof(Guid))
                {
                    StorageName = "workspace_id",
                    IsIndexed = true
                },
                new VectorStoreDataProperty(nameof(DocumentVectorRecord.EmbeddingInputHash), typeof(string))
                {
                    StorageName = "embedding_input_hash"
                },
                new VectorStoreVectorProperty(nameof(DocumentVectorRecord.Embedding), typeof(ReadOnlyMemory<float>), dimensions)
                {
                    StorageName = "embedding",
                    IndexKind = IndexKind.Flat,
                    DistanceFunction = DistanceFunction.CosineDistance
                }
            ]
        };
    }

    private static void ValidateDimensions(int dimensions)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(dimensions);
    }
}
