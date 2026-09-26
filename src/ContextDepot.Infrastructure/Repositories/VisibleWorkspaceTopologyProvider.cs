using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class VisibleWorkspaceTopologyProvider(ContextDepotDbContext db)
{
    private readonly Dictionary<Guid, VisibleWorkspaceTree> snapshots = [];

    internal async Task<VisibleWorkspaceTree> GetAsync(Guid depotId, CancellationToken cancellationToken)
    {
        if (snapshots.TryGetValue(depotId, out var snapshot))
        {
            return snapshot;
        }

        var workspaces = await db.Workspaces.AsNoTracking()
            .Where(workspace => workspace.DepotId == depotId)
            .ToArrayAsync(cancellationToken);
        var topology = new WorkspaceTopology(workspaces.Select(workspace =>
            new WorkspaceTreeNode(workspace.Id, workspace.ParentWorkspaceId, workspace.Slug)));
        snapshot = new VisibleWorkspaceTree(workspaces, topology);
        snapshots[depotId] = snapshot;
        return snapshot;
    }

    internal void Invalidate(Guid depotId) => snapshots.Remove(depotId);
}

internal sealed record VisibleWorkspaceTree(IReadOnlyList<Workspace> Workspaces, WorkspaceTopology Topology);
