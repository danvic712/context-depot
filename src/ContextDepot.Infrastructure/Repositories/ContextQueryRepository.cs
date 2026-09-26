using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Documents.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class ContextQueryRepository(
    ContextDepotDbContext db,
    VisibleWorkspaceTopologyProvider topologyProvider) : IContextQueryRepository
{
    public Task<ContextItem?> FindContextByIdAsync(
        Guid depotId,
        Guid contextId,
        CancellationToken cancellationToken) =>
        db.ContextItems.AsNoTracking()
            .SingleOrDefaultAsync(
                context => context.DepotId == depotId && context.Id == contextId,
                cancellationToken);

    public async Task<IReadOnlyList<ContextSearchCandidateRecord>> FindLexicalContextCandidatesAsync(
        ContextSearchQuery query,
        CancellationToken cancellationToken)
    {
        var workspacePaths = await FindWorkspacePathsAsync(query.DepotId, cancellationToken);
        var workspaceIds = query.WorkspaceIds?.ToArray();
        var kinds = query.Kinds?.ToArray();
        var contexts = db.ContextItems.AsNoTracking()
            .WhereRetrievableAt(query.Now)
            .Where(context => context.DepotId == query.DepotId);
        if (workspaceIds is not null)
        {
            contexts = contexts.Where(context => workspaceIds.Contains(context.WorkspaceId));
        }

        if (kinds is not null)
        {
            contexts = contexts.Where(context => kinds.Contains(context.Kind));
        }

        var candidates = await contexts
            .OrderByDescending(context => context.Importance)
            .ThenByDescending(context => context.UpdatedAt)
            .Take(query.CandidateLimit)
            .Select(context => new BootstrapContextCandidate(
                context.Id,
                context.DepotId,
                context.WorkspaceId,
                context.Kind,
                context.Key,
                context.Title,
                context.Content,
                context.TagsJson,
                context.Importance,
                context.Confidence,
                context.Status,
                context.VerificationStatus,
                context.ProvenanceTrust,
                context.SourceType,
                context.SourceAgent,
                context.SourceRef,
                context.SupersedesId,
                context.ExpiresAt,
                context.CreatedAt,
                context.UpdatedAt,
                context.MetadataJson))
            .ToListAsync(cancellationToken);
        return candidates
            .Select(candidate => new ContextSearchCandidateRecord(
                candidate,
                workspacePaths.GetValueOrDefault(candidate.WorkspaceId, string.Empty)))
            .ToArray();
    }

    public async Task<IReadOnlyList<DocumentSearchCandidateRecord>> FindLexicalDocumentCandidatesAsync(
        ContextSearchQuery query,
        CancellationToken cancellationToken)
    {
        var workspacePaths = await FindWorkspacePathsAsync(query.DepotId, cancellationToken);
        var workspaceIds = query.WorkspaceIds?.ToArray();
        var documents = db.DocumentChunks.AsNoTracking()
            .Where(chunk => chunk.DepotId == query.DepotId &&
                           chunk.Document != null &&
                           chunk.Document.Status == DocumentStatus.Active &&
                           chunk.Document.IndexStatus == DocumentIndexStatus.Indexed);
        if (workspaceIds is not null)
        {
            documents = documents.Where(chunk => workspaceIds.Contains(chunk.WorkspaceId));
        }

        var candidates = await documents
            .OrderByDescending(chunk => chunk.UpdatedAt)
            .ThenBy(chunk => chunk.DocumentId)
            .ThenBy(chunk => chunk.Ordinal)
            .Take(query.CandidateLimit)
            .Select(chunk => new BootstrapDocumentChunkCandidate(
                chunk.Id,
                chunk.DepotId,
                chunk.DocumentId,
                chunk.WorkspaceId,
                chunk.Ordinal,
                chunk.Document!.Path,
                chunk.Document.Title,
                chunk.HeadingPath,
                chunk.Content,
                chunk.ContentHash,
                chunk.UpdatedAt))
            .ToListAsync(cancellationToken);
        return candidates
            .Select(candidate => new DocumentSearchCandidateRecord(
                candidate,
                workspacePaths.GetValueOrDefault(candidate.WorkspaceId, string.Empty)))
            .ToArray();
    }

    private async Task<IReadOnlyDictionary<Guid, string>> FindWorkspacePathsAsync(
        Guid depotId,
        CancellationToken cancellationToken)
    {
        return (await topologyProvider.GetAsync(depotId, cancellationToken)).Topology.Paths;
    }

}
