namespace ContextDepot.Application.Documents.Dtos;

public sealed record DocumentContentModel(
    DocumentModel Metadata,
    string Content,
    string ContentHash);
