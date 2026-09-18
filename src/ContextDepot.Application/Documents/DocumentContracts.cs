using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Documents;

public sealed record UpsertDocumentCommand(
    string Workspace,
    string Path,
    string Title,
    string Content,
    string? ExpectedContentHash = null);

public sealed record DocumentChunkModel(Guid Id, int Ordinal, string HeadingPath, string Content, string ContentHash);

public sealed record DocumentModel(
    Guid Id,
    Guid OwnerId,
    Guid WorkspaceId,
    string Workspace,
    string Path,
    string Title,
    string ContentHash,
    string? IndexedContentHash,
    DocumentStatus Status,
    DocumentIndexStatus IndexStatus,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<DocumentChunkModel> Chunks);

public sealed record DocumentContentModel(
    DocumentModel Metadata,
    string Content,
    string ContentHash);

public interface IDocumentAppService
{
    Task<DocumentModel> UpsertAsync(UpsertDocumentCommand command, CancellationToken cancellationToken);

    Task<DocumentContentModel?> GetAsync(Guid documentId, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid documentId, CancellationToken cancellationToken);
}
