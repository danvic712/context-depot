using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class VisibleWorkspaceTopologyProvider(ContextDepotDbContext db)
{
    private VisibleWorkspaceTree? snapshot;

    internal async Task<VisibleWorkspaceTree> GetAsync(CancellationToken cancellationToken)
    {
        if (snapshot is not null)
        {
            return snapshot;
        }

        var workspaces = await db.Workspaces.AsNoTracking()
            .ToArrayAsync(cancellationToken);
        var topology = new WorkspaceTopology(workspaces.Select(workspace =>
            new WorkspaceTreeNode(workspace.Id, workspace.ParentWorkspaceId, workspace.Slug)));
        snapshot = new VisibleWorkspaceTree(workspaces, topology);
        return snapshot;
    }

    internal void Invalidate() => snapshot = null;
}

internal sealed record VisibleWorkspaceTree(IReadOnlyList<Workspace> Workspaces, WorkspaceTopology Topology);
