namespace ContextDepot.Application.Documents.Dtos;

public sealed record UpsertDocumentCommand(
    string Workspace,
    string Path,
    string Title,
    string Content,
    string? ExpectedContentHash = null);
