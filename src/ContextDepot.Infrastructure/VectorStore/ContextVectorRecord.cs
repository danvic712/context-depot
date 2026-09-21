namespace ContextDepot.Infrastructure.VectorStore;

internal sealed class ContextVectorRecord
{
    public required Guid ContextItemId { get; init; }

    public required Guid DepotId { get; init; }

    public required Guid WorkspaceId { get; init; }

    public required string Kind { get; init; }

    public required string EmbeddingInputHash { get; init; }

    public required ReadOnlyMemory<float> Embedding { get; init; }
}
