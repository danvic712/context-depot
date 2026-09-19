namespace ContextDepot.Application.Documents.Dtos;

public sealed record DocumentChunkWrite(
    Guid Id,
    int Ordinal,
    string HeadingPath,
    string Content,
    string ContentHash);
