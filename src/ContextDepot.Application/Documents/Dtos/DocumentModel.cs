using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;

namespace ContextDepot.Application.Documents.Dtos;

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
