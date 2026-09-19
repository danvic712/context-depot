namespace ContextDepot.Application.Embeddings.Dtos;

public sealed record DocumentChunkEmbeddingSource(
    string WorkspacePath,
    string DocumentPath,
    string Title,
    string HeadingPath,
    string Content);
