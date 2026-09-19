namespace ContextDepot.Application.Bootstrap.Dtos;

public sealed record DocumentExcerptModel(
    Guid DocumentId,
    Guid ChunkId,
    string Path,
    string HeadingPath,
    string Content,
    string ContentHash);
