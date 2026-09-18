using ContextDepot.Domain.Entities;

namespace ContextDepot.Application.Persistence;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid ownerId, Guid workspaceId, CancellationToken cancellationToken);

    Task<Workspace?> GetByPathAsync(Guid ownerId, string normalizedPath, CancellationToken cancellationToken);

    Task<IReadOnlyList<Workspace>> ListAsync(Guid ownerId, string? parentPath, CancellationToken cancellationToken);

    Task AddAsync(Workspace workspace, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IContextRepository
{
    Task<ContextItem?> GetByIdAsync(Guid ownerId, Guid contextId, CancellationToken cancellationToken);

    Task<ContextItem?> GetActiveByKeyAsync(Guid ownerId, Guid workspaceId, string key, CancellationToken cancellationToken);

    Task<bool> HasActiveDuplicateAsync(Guid ownerId, Guid workspaceId, ContextKind kind, string normalizedContent, CancellationToken cancellationToken);

    Task AddAsync(ContextItem context, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IDocumentRepository
{
    Task<Document?> GetByIdAsync(Guid ownerId, Guid documentId, CancellationToken cancellationToken);

    Task<Document?> GetByPathAsync(Guid ownerId, Guid workspaceId, string normalizedPath, CancellationToken cancellationToken);

    Task AddAsync(Document document, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IBootstrapRepository
{
    Task<IReadOnlyList<ContextItem>> GetActiveContextsAsync(Guid ownerId, CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentChunk>> GetIndexedDocumentChunksAsync(Guid ownerId, CancellationToken cancellationToken);
}
