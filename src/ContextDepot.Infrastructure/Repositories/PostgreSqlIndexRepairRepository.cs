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
    private readonly Dictionary<Guid, IReadOnlyDictionary<Guid, string>> workspacePathsByDepot = [];

    public async Task<IReadOnlyList<DocumentIndexRepairCandidate>> FindDocumentIndexRepairCandidatesAsync(
        Guid depotId,
        int limit,
        CancellationToken cancellationToken)
    {
        var rows = await db.Documents
            .AsNoTracking()
            .Where(x => x.DepotId == depotId &&
                        x.Status == DocumentStatus.Active &&
                        (x.IndexStatus == DocumentIndexStatus.Pending || x.IndexStatus == DocumentIndexStatus.Failed))
            .OrderBy(x => x.Id)
            .Take(limit)
            .Select(x => new
            {
                x.Id,
                x.DepotId,
                x.WorkspaceId,
                x.Path,
                x.Title
            })
            .ToListAsync(cancellationToken);
        if (rows.Count == 0)
        {
            return [];
        }

        var workspacePaths = await GetWorkspacePathsAsync(depotId, cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new DocumentIndexRepairCandidate(
                row.Id,
                row.DepotId,
                row.WorkspaceId,
                workspacePaths[row.WorkspaceId],
                row.Path,
                row.Title))
            .ToArray();
    }

    public async Task<IReadOnlyList<ContextEmbeddingRepairCandidate>> FindContextSourcePageAsync(
        Guid depotId,
        Guid? afterContextId,
        int limit,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        var query = db.ContextItems
            .AsNoTracking()
            .Where(x => x.DepotId == depotId &&
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
                x.DepotId,
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

        var workspacePaths = await GetWorkspacePathsAsync(depotId, cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new ContextEmbeddingRepairCandidate(
                row.ContextItemId,
                row.DepotId,
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
        Guid depotId,
        Guid? afterDocumentChunkId,
        int limit,
        CancellationToken cancellationToken)
    {
        var query = db.DocumentChunks
            .AsNoTracking()
            .Where(x => x.DepotId == depotId &&
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
                x.DepotId,
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

        var workspacePaths = await GetWorkspacePathsAsync(depotId, cancellationToken);
        return rows
            .Where(row => workspacePaths.ContainsKey(row.WorkspaceId))
            .Select(row => new DocumentEmbeddingRepairCandidate(
                row.DocumentChunkId,
                row.DocumentId,
                row.DepotId,
                row.WorkspaceId,
                workspacePaths[row.WorkspaceId],
                row.DocumentPath,
                row.Title,
                row.HeadingPath,
                row.Content))
            .ToArray();
    }

    public async Task MarkDocumentIndexFailedAsync(
        Guid depotId,
        Guid documentId,
        string errorCode,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(errorCode))
        {
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InternalError);
        }

        var document = await db.Documents.SingleOrDefaultAsync(
            x => x.DepotId == depotId && x.Id == documentId,
            cancellationToken);
        if (document?.Status != DocumentStatus.Active)
        {
            return;
        }

        document.MarkFailed(errorCode, timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
    }

    private async Task<IReadOnlyDictionary<Guid, string>> GetWorkspacePathsAsync(
        Guid depotId,
        CancellationToken cancellationToken)
    {
        if (workspacePathsByDepot.TryGetValue(depotId, out var cachedPaths))
        {
            return cachedPaths;
        }

        var workspaces = await db.Workspaces
            .AsNoTracking()
            .Where(x => x.DepotId == depotId)
            .ToListAsync(cancellationToken);
        var paths = WorkspacePath.BuildPaths(workspaces);
        workspacePathsByDepot[depotId] = paths;
        return paths;
    }
}
