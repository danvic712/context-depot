using System.Text.RegularExpressions;
using ContextDepot.Application.Overview;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Infrastructure.Repositories;

public sealed partial class OverviewRepository(ContextDepotDbContext db) : IOverviewRepository
{
    public async Task<ResourceCollection<WorkspaceSummary>> ListWorkspacesAsync(Guid depotId, int limit, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var contexts = db.ContextItems.AsNoTracking().Where(x => x.DepotId == depotId).WhereRetrievableAt(asOf);
        var documents = db.Documents.AsNoTracking().Where(x => x.DepotId == depotId && x.Status == DocumentStatus.Active);
        var rows = await db.Workspaces.AsNoTracking().Where(x => x.DepotId == depotId)
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.UpdatedAt,
                ContextCount = contexts.Count(c => c.WorkspaceId == x.Id),
                DocumentCount = documents.Count(d => d.WorkspaceId == x.Id),
                ContextUpdatedAt = contexts.Where(c => c.WorkspaceId == x.Id).Max(c => (DateTimeOffset?)c.UpdatedAt),
                DocumentUpdatedAt = documents.Where(d => d.WorkspaceId == x.Id).Max(d => (DateTimeOffset?)d.UpdatedAt)
            })
            .Select(x => new
            {
                x.Id,
                x.Name,
                x.Description,
                x.ContextCount,
                x.DocumentCount,
                ActivityAt = (x.ContextUpdatedAt ?? x.UpdatedAt) > (x.DocumentUpdatedAt ?? x.UpdatedAt)
                    ? ((x.ContextUpdatedAt ?? x.UpdatedAt) > x.UpdatedAt ? x.ContextUpdatedAt!.Value : x.UpdatedAt)
                    : ((x.DocumentUpdatedAt ?? x.UpdatedAt) > x.UpdatedAt ? x.DocumentUpdatedAt!.Value : x.UpdatedAt)
            })
            .OrderByDescending(x => x.ActivityAt).ThenBy(x => x.Id).Take(limit + 1)
            .ToArrayAsync(cancellationToken);
        var paths = await LoadPathsAsync(depotId, cancellationToken);
        return new(asOf, rows.Take(limit).Select(x => new WorkspaceSummary(x.Id, x.Name, x.Description,
            paths[x.Id], x.ContextCount, x.DocumentCount, x.ActivityAt)).ToArray(), rows.Length > limit);
    }

    public Task<ResourceCollection<KnowledgeSummary>> ListKnowledgeAsync(Guid depotId, int limit, DateTimeOffset asOf, CancellationToken cancellationToken) =>
        ListRecentKnowledgeAsync(depotId, limit, new(null, null, null), asOf, cancellationToken);

    /// <inheritdoc />
    public async Task<ResourceCollection<KnowledgeSummary>> ListRecentKnowledgeAsync(Guid depotId, int limit,
        RecentKnowledgeFilter filter, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var paths = await LoadPathsAsync(depotId, cancellationToken);
        Guid? workspaceId = null;
        if (filter.Workspace is not null)
        {
            workspaceId = paths.Where(x => x.Value == filter.Workspace).Select(x => (Guid?)x.Key).FirstOrDefault();
            if (workspaceId is null) throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceNotFound);
        }
        var contextQuery = db.ContextItems.AsNoTracking().Where(x => x.DepotId == depotId).WhereRetrievableAt(asOf);
        var documentQuery = db.Documents.AsNoTracking().Where(x => x.DepotId == depotId && x.Status == DocumentStatus.Active);
        if (workspaceId is not null)
        {
            contextQuery = contextQuery.Where(x => x.WorkspaceId == workspaceId);
            documentQuery = documentQuery.Where(x => x.WorkspaceId == workspaceId);
        }
        if (filter.Type == "documents") contextQuery = contextQuery.Where(_ => false);
        if (filter.Type == "contexts" || filter.Kind is not null) documentQuery = documentQuery.Where(_ => false);
        if (filter.Kind is not null) contextQuery = contextQuery.Where(x => x.Kind == filter.Kind);
        var contexts = await contextQuery
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Take(limit + 1)
            .Select(x => new { x.Id, x.Kind, x.Title, x.Key, x.Content, x.WorkspaceId, x.UpdatedAt })
            .ToArrayAsync(cancellationToken);
        var documents = await documentQuery
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Take(limit + 1)
            .Select(x => new { x.Id, x.Title, x.Path, x.WorkspaceId, x.UpdatedAt, x.IndexStatus })
            .ToArrayAsync(cancellationToken);
        var merged = contexts.Select(x => new KnowledgeSummary(x.Id, "context", x.Kind,
                ContextTitle(x.Title, x.Key, x.Content), new(x.WorkspaceId, paths[x.WorkspaceId]), x.UpdatedAt, null))
            .Concat(documents.Select(x => new KnowledgeSummary(x.Id, "document", null,
                string.IsNullOrWhiteSpace(x.Title) ? x.Path : x.Title, new(x.WorkspaceId, paths[x.WorkspaceId]), x.UpdatedAt, x.IndexStatus)))
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Type, StringComparer.Ordinal).ThenBy(x => x.Id)
            .Take(limit + 1).ToArray();
        return new(asOf, merged.Take(limit).ToArray(), merged.Length > limit);
    }

    public async Task<bool> CreateWorkspaceAsync(Guid depotId, Guid id, string name, string path, string? description, DateTimeOffset now, CancellationToken cancellationToken)
    {
        var workspace = new Workspace(id, depotId, null, name, path, description, now);
        db.Workspaces.Add(workspace);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException
        { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_workspaces_root_slug" })
        {
            db.Entry(workspace).State = EntityState.Detached;
            return false;
        }
    }

    private async Task<IReadOnlyDictionary<Guid, string>> LoadPathsAsync(Guid depotId, CancellationToken cancellationToken)
    {
        var nodes = await db.Workspaces.AsNoTracking().Where(x => x.DepotId == depotId)
            .Select(x => new WorkspaceTreeNode(x.Id, x.ParentWorkspaceId, x.Slug)).ToArrayAsync(cancellationToken);
        return new WorkspaceTopology(nodes).Paths;
    }

    internal static string ContextTitle(string? title, string? key, string content)
    {
        if (!string.IsNullOrWhiteSpace(title)) return title;
        if (!string.IsNullOrWhiteSpace(key)) return key;
        var paragraph = content.TrimStart().Split(["\r\n\r\n", "\n\n"], StringSplitOptions.None)[0];
        var plain = MarkdownLink().Replace(paragraph, "$1");
        plain = MarkdownMarkup().Replace(plain, " ").Trim();
        return string.Concat(plain.EnumerateRunes().Take(80).Select(x => x.ToString()));
    }

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)")]
    private static partial Regex MarkdownLink();
    [GeneratedRegex(@"<[^>]*>|[\s#*`_>~]+")]
    private static partial Regex MarkdownMarkup();
}
