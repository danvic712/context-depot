using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class BootstrapRepository(ContextDepotDbContext db) : IBootstrapRepository
{
    public async Task<IReadOnlyList<BootstrapWorkspaceCandidate>> FindScopeCandidatesAsync(
        BootstrapQuery query,
        CancellationToken cancellationToken) =>
        await ApplyWorkspaceScope(
                db.Workspaces.AsNoTracking().Where(x => x.DepotId == query.DepotId),
                query.WorkspaceIds)
            .OrderBy(x => x.Slug)
            .Take(query.CandidateLimit)
            .Select(x => new BootstrapWorkspaceCandidate(
                x.Id,
                x.DepotId,
                x.ParentWorkspaceId,
                x.Name,
                x.Slug))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BootstrapContextCandidate>> FindContextCandidatesAsync(
        BootstrapQuery query,
        CancellationToken cancellationToken) =>
        await ApplyWorkspaceScope(
                db.ContextItems.AsNoTracking()
                    .Where(x => x.DepotId == query.DepotId && x.Status == ContextStatus.Active &&
                                (x.ExpiresAt == null || x.ExpiresAt > query.Now)),
                query.WorkspaceIds)
            .OrderByDescending(x => x.Importance)
            .ThenByDescending(x => x.UpdatedAt)
            .Take(query.CandidateLimit)
            .Select(x => new BootstrapContextCandidate(
                x.Id,
                x.DepotId,
                x.WorkspaceId,
                x.Kind,
                x.Key,
                x.Title,
                x.Content,
                x.TagsJson,
                x.Importance,
                x.Confidence,
                x.Status,
                x.VerificationStatus,
                x.ProvenanceTrust,
                x.SourceType,
                x.SourceAgent,
                x.SourceRef,
                x.SupersedesId,
                x.ExpiresAt,
                x.CreatedAt,
                x.UpdatedAt,
                x.MetadataJson))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BootstrapDocumentChunkCandidate>> FindDocumentCandidatesAsync(
        BootstrapQuery query,
        CancellationToken cancellationToken) =>
        await ApplyWorkspaceScope(
                db.DocumentChunks.AsNoTracking()
                    .Where(x => x.DepotId == query.DepotId && x.Document != null &&
                                x.Document.Status == DocumentStatus.Active &&
                                x.Document.IndexStatus == DocumentIndexStatus.Indexed),
                query.WorkspaceIds)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.DocumentId)
            .ThenBy(x => x.Ordinal)
            .Take(query.CandidateLimit)
            .Select(x => new BootstrapDocumentChunkCandidate(
                x.Id,
                x.DepotId,
                x.DocumentId,
                x.WorkspaceId,
                x.Ordinal,
                x.Document!.Path,
                x.Document.Title,
                x.HeadingPath,
                x.Content,
                x.ContentHash,
                x.UpdatedAt))
            .ToListAsync(cancellationToken);

    private static IQueryable<Workspace> ApplyWorkspaceScope(
        IQueryable<Workspace> query,
        IReadOnlySet<Guid>? workspaceIds)
    {
        if (workspaceIds is null) return query;
        var ids = workspaceIds.ToArray();
        return query.Where(workspace => ids.Contains(workspace.Id));
    }

    private static IQueryable<ContextItem> ApplyWorkspaceScope(
        IQueryable<ContextItem> query,
        IReadOnlySet<Guid>? workspaceIds)
    {
        if (workspaceIds is null) return query;
        var ids = workspaceIds.ToArray();
        return query.Where(context => ids.Contains(context.WorkspaceId));
    }

    private static IQueryable<DocumentChunk> ApplyWorkspaceScope(
        IQueryable<DocumentChunk> query,
        IReadOnlySet<Guid>? workspaceIds)
    {
        if (workspaceIds is null) return query;
        var ids = workspaceIds.ToArray();
        return query.Where(chunk => ids.Contains(chunk.WorkspaceId));
    }
}
