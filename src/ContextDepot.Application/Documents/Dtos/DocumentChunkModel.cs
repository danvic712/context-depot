namespace ContextDepot.Application.Documents.Dtos;

public sealed record DocumentChunkModel(Guid Id, int Ordinal, string HeadingPath, string Content, string ContentHash);
