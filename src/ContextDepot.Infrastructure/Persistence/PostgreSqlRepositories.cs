using ContextDepot.Application.Persistence;
using ContextDepot.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Persistence;

public sealed class PostgreSqlWorkspaceRepository(ContextDepotDbContext db) : IWorkspaceRepository
{
    public Task<Workspace?> GetByIdAsync(Guid ownerId, Guid workspaceId, CancellationToken cancellationToken) =>
        db.Workspaces.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == workspaceId, cancellationToken);

    public Task<Workspace?> GetByPathAsync(Guid ownerId, string normalizedPath, CancellationToken cancellationToken)
    {
        var slugs = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        IQueryable<Workspace> query = db.Workspaces.AsNoTracking().Where(x => x.OwnerId == ownerId);
        return FindByPathAsync(query, slugs, cancellationToken);
    }

    public async Task<IReadOnlyList<Workspace>> ListAsync(Guid ownerId, string? parentPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentPath))
        {
            return await db.Workspaces.AsNoTracking().Where(x => x.OwnerId == ownerId && x.ParentWorkspaceId == null).OrderBy(x => x.Slug).ToListAsync(cancellationToken);
        }

        var parent = await GetByPathAsync(ownerId, parentPath, cancellationToken);
        if (parent is null)
        {
            return [];
        }

        return await db.Workspaces.AsNoTracking().Where(x => x.OwnerId == ownerId && x.ParentWorkspaceId == parent.Id).OrderBy(x => x.Slug).ToListAsync(cancellationToken);
    }

    public Task AddAsync(Workspace workspace, CancellationToken cancellationToken)
    {
        db.Workspaces.Add(workspace);
        return Task.CompletedTask;
    }

    public void Update(Workspace workspace) => db.Workspaces.Update(workspace);

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);

    private static async Task<Workspace?> FindByPathAsync(IQueryable<Workspace> query, IReadOnlyList<string> slugs, CancellationToken cancellationToken)
    {
        if (slugs.Count == 0)
        {
            return null;
        }

        Workspace? parent = null;
        foreach (var slug in slugs)
        {
            parent = await query.SingleOrDefaultAsync(x => x.Slug == slug && x.ParentWorkspaceId == (parent == null ? null : parent.Id), cancellationToken);
            if (parent is null)
            {
                return null;
            }
        }

        return parent;
    }
}

public sealed class PostgreSqlContextRepository(ContextDepotDbContext db) : IContextRepository
{
    public Task<ContextItem?> GetByIdAsync(Guid ownerId, Guid contextId, CancellationToken cancellationToken) =>
        db.ContextItems.SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == contextId, cancellationToken);

    public Task<ContextItem?> GetActiveByKeyAsync(Guid ownerId, Guid workspaceId, string key, CancellationToken cancellationToken) =>
        db.ContextItems.SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.WorkspaceId == workspaceId && x.Key == key && x.Status == ContextStatus.Active, cancellationToken);

    public Task<bool> HasActiveDuplicateAsync(Guid ownerId, Guid workspaceId, ContextKind kind, string normalizedContent, CancellationToken cancellationToken) =>
        db.ContextItems.AnyAsync(x => x.OwnerId == ownerId && x.WorkspaceId == workspaceId && x.Kind == kind && x.Status == ContextStatus.Active && x.Content.ToLower() == normalizedContent, cancellationToken);

    public Task AddAsync(ContextItem context, CancellationToken cancellationToken)
    {
        db.ContextItems.Add(context);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

public sealed class PostgreSqlDocumentRepository(ContextDepotDbContext db) : IDocumentRepository
{
    public Task<Document?> GetByIdAsync(Guid ownerId, Guid documentId, CancellationToken cancellationToken) =>
        db.Documents.Include(x => x.Chunks).SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == documentId, cancellationToken);

    public Task<Document?> GetByPathAsync(Guid ownerId, Guid workspaceId, string normalizedPath, CancellationToken cancellationToken) =>
        db.Documents.Include(x => x.Chunks).SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.WorkspaceId == workspaceId && x.Path == normalizedPath, cancellationToken);

    public Task AddAsync(Document document, CancellationToken cancellationToken)
    {
        db.Documents.Add(document);
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => db.SaveChangesAsync(cancellationToken);
}

public sealed class PostgreSqlBootstrapRepository(ContextDepotDbContext db) : IBootstrapRepository
{
    public async Task<IReadOnlyList<ContextItem>> GetActiveContextsAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await db.ContextItems.AsNoTracking().Where(x => x.OwnerId == ownerId && x.Status == ContextStatus.Active && (x.ExpiresAt == null || x.ExpiresAt > DateTimeOffset.UtcNow)).OrderByDescending(x => x.Importance).ThenByDescending(x => x.UpdatedAt).ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DocumentChunk>> GetIndexedDocumentChunksAsync(Guid ownerId, CancellationToken cancellationToken) =>
        await db.DocumentChunks.AsNoTracking().Where(x => x.OwnerId == ownerId && x.Document != null && x.Document.Status == DocumentStatus.Active && x.Document.IndexStatus == DocumentIndexStatus.Indexed).OrderBy(x => x.DocumentId).ThenBy(x => x.Ordinal).ToListAsync(cancellationToken);
}
