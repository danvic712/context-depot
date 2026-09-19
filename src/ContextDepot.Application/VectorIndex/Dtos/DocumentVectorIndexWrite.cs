namespace ContextDepot.Application.VectorIndex.Dtos;

public sealed record DocumentVectorIndexWrite(
    Guid DocumentChunkId,
    Guid DocumentId,
    Guid OwnerId,
    Guid WorkspaceId,
    string EmbeddingInputHash,
    ReadOnlyMemory<float> Embedding);
