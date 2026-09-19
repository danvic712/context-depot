using ContextDepot.Domain.Contexts.Enums;

namespace ContextDepot.Application.Embeddings.Dtos;

public sealed record ContextEmbeddingSource(
    string WorkspacePath,
    ContextKind Kind,
    string? Key,
    string? Title,
    IReadOnlyList<string>? Tags,
    string Content);
