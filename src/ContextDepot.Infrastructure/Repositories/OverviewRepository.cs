using System.Text.RegularExpressions;
using ContextDepot.Application.Overview;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;
using Npgsql;

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

    public async Task<ResourceCollection<KnowledgeSummary>> ListKnowledgeAsync(Guid depotId, int limit, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var contexts = await db.ContextItems.AsNoTracking().Where(x => x.DepotId == depotId).WhereRetrievableAt(asOf)
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Take(limit + 1)
            .Select(x => new { x.Id, x.Kind, x.Title, x.Key, x.Content, x.WorkspaceId, x.UpdatedAt })
            .ToArrayAsync(cancellationToken);
        var documents = await db.Documents.AsNoTracking().Where(x => x.DepotId == depotId && x.Status == DocumentStatus.Active)
            .OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Id).Take(limit + 1)
            .Select(x => new { x.Id, x.Title, x.Path, x.WorkspaceId, x.UpdatedAt, x.IndexStatus })
            .ToArrayAsync(cancellationToken);
        var paths = await LoadPathsAsync(depotId, cancellationToken);
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
