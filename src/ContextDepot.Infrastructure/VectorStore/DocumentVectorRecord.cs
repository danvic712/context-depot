namespace ContextDepot.Infrastructure.VectorStore;

internal sealed class DocumentVectorRecord
{
    public required Guid DocumentChunkId { get; init; }

    public required Guid DocumentId { get; init; }

    public required Guid OwnerId { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string EmbeddingInputHash { get; init; }

    public required ReadOnlyMemory<float> Embedding { get; init; }
}
