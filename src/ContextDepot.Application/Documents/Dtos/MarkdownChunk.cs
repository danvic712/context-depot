namespace ContextDepot.Application.Documents.Dtos;

public sealed record MarkdownChunk(string HeadingPath, string Content, string ContentHash);
