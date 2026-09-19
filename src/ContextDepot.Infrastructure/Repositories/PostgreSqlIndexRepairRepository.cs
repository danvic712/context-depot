using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class PostgreSqlIndexRepairRepository(
    ContextDepotDbContext db,
    TimeProvider timeProvider) : IIndexRepairRepository
{
    public async Task<IReadOnlyList<DocumentIndexRepairCandidate>> FindDocumentIndexRepairCandidatesAsync(
        Guid ownerId,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await db.Documents
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId &&
                        x.Status == DocumentStatus.Active &&
                        (x.IndexStatus == DocumentIndexStatus.Pending || x.IndexStatus == DocumentIndexStatus.Failed))
            .OrderBy(x => x.Id)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.OwnerId,
                x.WorkspaceId,
                x.Path,
                x.Title
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var workspacePaths = await GetWorkspacePathsAsync(ownerId, cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new DocumentIndexRepairCandidate(
                row.Id,
                row.OwnerId,
                row.WorkspaceId,
                workspacePaths[row.WorkspaceId],
                row.Path,
                row.Title))
            .ToArray();
    }

    public async Task<IReadOnlyList<ContextEmbeddingRepairCandidate>> FindContextSourcePageAsync(
        Guid ownerId,
        Guid? afterContextId,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var query = db.ContextItems
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId &&
                        x.Status == ContextStatus.Active &&
                        (x.ExpiresAt == null || x.ExpiresAt > now));
        if (afterContextId is Guid cursor)
        {
            query = query.Where(x => x.Id.CompareTo(cursor) > 0);
        }

        var rows = await query
            .OrderBy(x => x.Id)
            .Take(limit)
            .Select(x => new
            {
                ContextItemId = x.Id,
                x.OwnerId,
                x.WorkspaceId,
                x.Kind,
                x.Key,
                x.Title,
                x.TagsJson,
                x.Content
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var workspacePaths = await GetWorkspacePathsAsync(ownerId, cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new ContextEmbeddingRepairCandidate(
                row.ContextItemId,
                row.OwnerId,
                row.WorkspaceId,
                workspacePaths[row.WorkspaceId],
                row.Kind,
                row.Key,
                row.Title,
                row.TagsJson,
                row.Content))
            .ToArray();
    }

    public async Task<IReadOnlyList<DocumentEmbeddingRepairCandidate>> FindDocumentChunkSourcePageAsync(
        Guid ownerId,
        Guid? afterDocumentChunkId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.DocumentChunks
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId &&
                        x.Document != null &&
                        x.Document.Status == DocumentStatus.Active &&
                        x.Document.IndexStatus == DocumentIndexStatus.Indexed);
        if (afterDocumentChunkId is Guid cursor)
        {
            query = query.Where(x => x.Id.CompareTo(cursor) > 0);
        }

        var rows = await query
            .OrderBy(x => x.Id)
            .Take(limit)
            .Select(x => new
            {
                DocumentChunkId = x.Id,
                x.DocumentId,
                x.OwnerId,
                x.WorkspaceId,
                DocumentPath = x.Document!.Path,
                Title = x.Document.Title,
                x.HeadingPath,
                x.Content
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var workspacePaths = await GetWorkspacePathsAsync(ownerId, cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new DocumentEmbeddingRepairCandidate(
                row.DocumentChunkId,
                row.DocumentId,
                row.OwnerId,
                row.WorkspaceId,
                workspacePaths[row.WorkspaceId],
                row.DocumentPath,
                row.Title,
                row.HeadingPath,
                row.Content))
            .ToArray();
    }

    public async Task MarkDocumentIndexFailedAsync(
        Guid ownerId,
        Guid documentId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InternalError);
        }

        var document = await db.Documents.SingleOrDefaultAsync(
            x => x.OwnerId == ownerId && x.Id == documentId,
            cancellationToken);
        if (document?.Status != DocumentStatus.Active)
        {
            return;
        }

        document.MarkFailed(errorCode, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetWorkspacePathsAsync(
        Guid ownerId,
        CancellationToken cancellationToken)
    {
        var workspaces = await db.Workspaces
            .AsNoTracking()
            .Where(x => x.OwnerId == ownerId)
            .ToListAsync(cancellationToken);
        return WorkspacePath.BuildPaths(workspaces);
    }
}
