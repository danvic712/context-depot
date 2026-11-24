using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Documents.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class PostgreSqlIndexRepairRepository(
    ContextDepotDbContext db,
    TimeProvider timeProvider) : IIndexRepairRepository
{
    private IReadOnlyDictionary<Guid, string>? workspacePaths;

    public async Task<IReadOnlyList<DocumentIndexRepairCandidate>> FindDocumentIndexRepairCandidatesAsync(
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await db.Documents
            .AsNoTracking()
            .Where(x => x.Status == DocumentStatus.Active &&
                        (x.IndexStatus == DocumentIndexStatus.Pending || x.IndexStatus == DocumentIndexStatus.Failed))
            .OrderBy(x => x.UpdatedAt)
            .ThenBy(x => x.Id)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.WorkspaceId,
                x.Path,
                x.Title,
                StoragePath = x.Workspace!.StoragePath
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var workspacePaths = await GetWorkspacePathsAsync(cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new DocumentIndexRepairCandidate(
                row.Id,
                row.WorkspaceId,
                workspacePaths[row.WorkspaceId],
                row.Path,
                row.Title,
                row.StoragePath))
            .ToArray();
    }

    public async Task<IReadOnlyList<ContextEmbeddingRepairCandidate>> FindContextSourcePageAsync(
        Guid? afterContextId,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var query = db.ContextItems
            .AsNoTracking()
            .WhereRetrievableAt(now);
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

        var workspacePaths = await GetWorkspacePathsAsync(cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new ContextEmbeddingRepairCandidate(
                row.ContextItemId,
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
        Guid? afterDocumentChunkId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.DocumentChunks
            .AsNoTracking()
            .Where(x => x.Document != null &&
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

        var workspacePaths = await GetWorkspacePathsAsync(cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new DocumentEmbeddingRepairCandidate(
                row.DocumentChunkId,
                row.DocumentId,
                row.WorkspaceId,
                workspacePaths[row.WorkspaceId],
                row.DocumentPath,
                row.Title,
                row.HeadingPath,
                row.Content))
            .ToArray();
    }

    public async Task MarkDocumentIndexFailedAsync(
        Guid documentId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InternalError);
        }

        var document = await db.Documents.SingleOrDefaultAsync(
            x => x.Id == documentId,
            cancellationToken);
        if (document?.Status != DocumentStatus.Active)
        {
            return;
        }

        document.MarkFailed(errorCode, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetWorkspacePathsAsync(
        CancellationToken cancellationToken)
    {
        if (workspacePaths is { } cachedPaths)
        {
            return cachedPaths;
        }

        var workspaces = await db.Workspaces
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        var paths = WorkspacePath.BuildPaths(workspaces);
        workspacePaths = paths;
        return paths;
    }
}
