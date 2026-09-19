using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class BootstrapRepository(ContextDepotDbContext db) : IBootstrapRepository
{
    public async Task<IReadOnlyList<BootstrapWorkspaceCandidate>> FindScopeCandidatesAsync(
        BootstrapQuery query,
        CancellationToken cancellationToken) =>
        await db.Workspaces.AsNoTracking()
            .Where(x => x.OwnerId == query.OwnerId)
            .OrderBy(x => x.Slug)
            .Take(query.CandidateLimit)
            .Select(x => new BootstrapWorkspaceCandidate(
                x.Id,
                x.OwnerId,
                x.ParentWorkspaceId,
                x.Name,
                x.Slug))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<BootstrapContextCandidate>> FindContextCandidatesAsync(
        BootstrapQuery query,
        CancellationToken cancellationToken) =>
        await db.ContextItems.AsNoTracking()
            .Where(x => x.OwnerId == query.OwnerId && x.Status == ContextStatus.Active &&
                        (x.ExpiresAt == null || x.ExpiresAt > query.Now))
            .OrderByDescending(x => x.Importance)
            .ThenByDescending(x => x.UpdatedAt)
            .Take(query.CandidateLimit)
            .Select(x => new BootstrapContextCandidate(
                x.Id,
                x.OwnerId,
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
        await db.DocumentChunks.AsNoTracking()
            .Where(x => x.OwnerId == query.OwnerId && x.Document != null &&
                        x.Document.Status == DocumentStatus.Active &&
                        x.Document.IndexStatus == DocumentIndexStatus.Indexed)
            .OrderByDescending(x => x.UpdatedAt)
            .ThenBy(x => x.DocumentId)
            .ThenBy(x => x.Ordinal)
            .Take(query.CandidateLimit)
            .Select(x => new BootstrapDocumentChunkCandidate(
                x.Id,
                x.OwnerId,
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
}
