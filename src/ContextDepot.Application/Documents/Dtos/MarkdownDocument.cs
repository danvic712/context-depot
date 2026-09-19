namespace ContextDepot.Application.Documents.Dtos;

public sealed record MarkdownDocument(string Path, string Content, string ContentHash);
