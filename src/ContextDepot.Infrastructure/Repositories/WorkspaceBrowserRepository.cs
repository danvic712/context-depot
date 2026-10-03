using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Overview;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents.Enums;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class WorkspaceBrowserRepository(ContextDepotDbContext db) : IWorkspaceBrowserRepository
{
    public async Task<WorkspaceDirectory> BrowseAsync(Guid depotId, Guid? parentId, int page, int pageSize,
        DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var query = Summaries(depotId, asOf).Where(x => x.ParentId == parentId);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.ActivityAt).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        var nodes = await LoadNodesAsync(depotId, cancellationToken);
        var paths = Topology(nodes).Paths;
        return new(asOf, rows.Select(x => ToItem(x, paths[x.Id])).ToArray(), total, page, pageSize);
    }

    public async Task<WorkspaceDetail?> GetAsync(Guid depotId, Guid id, DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var row = await Summaries(depotId, asOf).SingleOrDefaultAsync(x => x.Id == id, cancellationToken);
        if (row is null) return null;
        var nodes = await LoadNodesAsync(depotId, cancellationToken);
        var paths = Topology(nodes).Paths;
        var byId = nodes.ToDictionary(x => x.Id);
        var ancestors = new List<WorkspaceAncestor>();
        var visited = new HashSet<Guid> { id };
        var parentId = row.ParentId;
        while (parentId is Guid ancestorId && visited.Add(ancestorId) && byId.TryGetValue(ancestorId, out var node))
        {
            ancestors.Add(new(node.Id, node.Name, paths[node.Id]));
            parentId = node.ParentId;
        }
        ancestors.Reverse();
        return new(ToItem(row, paths[row.Id]), ancestors);
    }

    public async Task<WorkspaceKnowledge?> ListKnowledgeAsync(Guid depotId, Guid id, int page, int pageSize,
        DateTimeOffset asOf, CancellationToken cancellationToken)
    {
        var nodes = await LoadNodesAsync(depotId, cancellationToken);
        if (!nodes.Any(node => node.Id == id)) return null;
        var contexts = db.ContextItems.AsNoTracking().Where(x => x.DepotId == depotId && x.WorkspaceId == id)
            .WhereRetrievableAt(asOf).Select(x => new KnowledgeRow
            {
                Id = x.Id, Type = "context", Kind = x.Kind.ToString(), Title = x.Title, Key = x.Key,
                Content = x.Content, UpdatedAt = x.UpdatedAt, IndexStatus = null
            });
        var documents = db.Documents.AsNoTracking()
            .Where(x => x.DepotId == depotId && x.WorkspaceId == id && x.Status == DocumentStatus.Active)
            .Select(x => new KnowledgeRow
            {
                Id = x.Id, Type = "document", Kind = null, Title = x.Title, Key = x.Path,
                Content = "", UpdatedAt = x.UpdatedAt, IndexStatus = x.IndexStatus.ToString()
            });
        var query = contexts.Concat(documents);
        var total = await query.CountAsync(cancellationToken);
        var rows = await query.OrderByDescending(x => x.UpdatedAt).ThenBy(x => x.Type).ThenBy(x => x.Id)
            .Skip((page - 1) * pageSize).Take(pageSize).ToArrayAsync(cancellationToken);
        var path = Topology(nodes).Paths[id];
        return new(asOf, rows.Select(x => new KnowledgeSummary(x.Id, x.Type,
            x.Kind is null ? null : Enum.Parse<ContextKind>(x.Kind, true),
            x.Type == "context" ? OverviewRepository.ContextTitle(x.Title, x.Key, x.Content)
                : string.IsNullOrWhiteSpace(x.Title) ? x.Key! : x.Title,
            new(id, path), x.UpdatedAt,
            x.IndexStatus is null ? null : Enum.Parse<DocumentIndexStatus>(x.IndexStatus, true))).ToArray(), total, page, pageSize);
    }

    private IQueryable<SummaryRow> Summaries(Guid depotId, DateTimeOffset asOf)
    {
        var contexts = db.ContextItems.AsNoTracking().Where(x => x.DepotId == depotId).WhereRetrievableAt(asOf);
        var documents = db.Documents.AsNoTracking().Where(x => x.DepotId == depotId && x.Status == DocumentStatus.Active);
        var workspaces = db.Workspaces.AsNoTracking().Where(x => x.DepotId == depotId);
        return workspaces.Select(x => new
        {
            x.Id, x.Name, x.Description, x.ParentWorkspaceId, x.UpdatedAt,
            ContextCount = contexts.Count(c => c.WorkspaceId == x.Id),
            DocumentCount = documents.Count(d => d.WorkspaceId == x.Id),
            SubspaceCount = workspaces.Count(w => w.ParentWorkspaceId == x.Id),
            ContextUpdatedAt = contexts.Where(c => c.WorkspaceId == x.Id).Max(c => (DateTimeOffset?)c.UpdatedAt),
            DocumentUpdatedAt = documents.Where(d => d.WorkspaceId == x.Id).Max(d => (DateTimeOffset?)d.UpdatedAt)
        }).Select(x => new SummaryRow
        {
            Id = x.Id, Name = x.Name, Description = x.Description, ParentId = x.ParentWorkspaceId,
            ContextCount = x.ContextCount, DocumentCount = x.DocumentCount, SubspaceCount = x.SubspaceCount,
            ActivityAt = (x.ContextUpdatedAt ?? x.UpdatedAt) > (x.DocumentUpdatedAt ?? x.UpdatedAt)
                ? ((x.ContextUpdatedAt ?? x.UpdatedAt) > x.UpdatedAt ? x.ContextUpdatedAt!.Value : x.UpdatedAt)
                : ((x.DocumentUpdatedAt ?? x.UpdatedAt) > x.UpdatedAt ? x.DocumentUpdatedAt!.Value : x.UpdatedAt)
        });
    }

    private Task<Node[]> LoadNodesAsync(Guid depotId, CancellationToken cancellationToken) =>
        db.Workspaces.AsNoTracking().Where(x => x.DepotId == depotId)
            .Select(x => new Node(x.Id, x.ParentWorkspaceId, x.Name, x.Slug)).ToArrayAsync(cancellationToken);

    private static WorkspaceTopology Topology(IEnumerable<Node> nodes) =>
        new(nodes.Select(x => new WorkspaceTreeNode(x.Id, x.ParentId, x.Slug)));

    private static WorkspaceDirectoryItem ToItem(SummaryRow row, string path) =>
        new(row.Id, row.Name, row.Description, path, row.ContextCount, row.DocumentCount, row.SubspaceCount, row.ActivityAt);

    private sealed record Node(Guid Id, Guid? ParentId, string Name, string Slug);
    private sealed class KnowledgeRow
    {
        public Guid Id { get; init; }
        public string Type { get; init; } = "";
        public string? Kind { get; init; }
        public string? Title { get; init; }
        public string? Key { get; init; }
        public string Content { get; init; } = "";
        public DateTimeOffset UpdatedAt { get; init; }
        public string? IndexStatus { get; init; }
    }
    private sealed class SummaryRow
    {
        public Guid Id { get; init; }
        public string Name { get; init; } = string.Empty;
        public string? Description { get; init; }
        public Guid? ParentId { get; init; }
        public int ContextCount { get; init; }
        public int DocumentCount { get; init; }
        public int SubspaceCount { get; init; }
        public DateTimeOffset ActivityAt { get; init; }
    }
}
