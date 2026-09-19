using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.VectorIndex.Dtos;

public sealed record ContextVectorIndexWrite(
    Guid ContextItemId,
    Guid OwnerId,
    Guid WorkspaceId,
    ContextKind Kind,
    string EmbeddingInputHash,
    ReadOnlyMemory<float> Embedding);
