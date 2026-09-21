namespace ContextDepot.Application.VectorIndex.Dtos;

public sealed record DocumentVectorIndexWrite(
    Guid DocumentChunkId,
    Guid DocumentId,
    Guid DepotId,
    Guid WorkspaceId,
    string EmbeddingInputHash,
    ReadOnlyMemory<float> Embedding);
