using System.Data;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed partial class WorkspaceRepository(
    ContextDepotDbContext db,
    IIdGenerator idGenerator,
    IWorkspaceAccessContext workspaceAccess,
    VisibleWorkspaceTopologyProvider topologyProvider) : IWorkspaceRepository
{
    public async Task<WorkspaceTopology> LoadVisibleTopologyAsync(Guid depotId, CancellationToken cancellationToken) =>
        (await topologyProvider.GetAsync(depotId, cancellationToken)).Topology;

    public Task<Workspace?> GetByIdAsync(Guid depotId, Guid workspaceId, CancellationToken cancellationToken) =>
        workspaceAccess.CanAccess(workspaceId)
            ? db.Workspaces.AsNoTracking().SingleOrDefaultAsync(
                x => x.DepotId == depotId && x.Id == workspaceId,
                cancellationToken)
            : Task.FromResult<Workspace?>(null);

    public async Task<WorkspacePathLookup?> GetByIdWithPathAsync(Guid depotId, Guid workspaceId, CancellationToken cancellationToken)
    {
        if (!workspaceAccess.CanAccess(workspaceId))
        {
            return null;
        }

        var tree = await topologyProvider.GetAsync(depotId, cancellationToken);
        var workspace = tree.Workspaces.SingleOrDefault(x => x.Id == workspaceId);
        if (workspace is null)
        {
            return null;
        }

        return new WorkspacePathLookup(workspace, tree.Topology.Paths[workspace.Id]);
    }

    public async Task<Workspace?> GetByPathAsync(Guid depotId, string normalizedPath, CancellationToken cancellationToken)
    {
        var tree = await topologyProvider.GetAsync(depotId, cancellationToken);
        if (!tree.Topology.TryGetId(normalizedPath, out var workspaceId) || !workspaceAccess.CanAccess(workspaceId))
        {
            return null;
        }

        return tree.Workspaces.SingleOrDefault(workspace => workspace.Id == workspaceId);
    }

    public async Task<IReadOnlyList<WorkspacePathLookup>> ListWithPathsAsync(Guid depotId, string? parentPath, CancellationToken cancellationToken)
    {
        var tree = await topologyProvider.GetAsync(depotId, cancellationToken);
        Guid? parentId = null;
        if (!string.IsNullOrWhiteSpace(parentPath))
        {
            if (!tree.Topology.TryGetId(parentPath, out var resolvedParentId))
            {
                return [];
            }

            parentId = resolvedParentId;
        }

        return tree.Workspaces
            .Where(x => x.ParentWorkspaceId == parentId && workspaceAccess.CanAccess(x.Id))
            .OrderBy(x => x.Slug)
            .Select(x => new WorkspacePathLookup(x, tree.Topology.Paths[x.Id]))
            .ToArray();
    }

    // Compatibility for existing repository callers; all decisions live in Application.
    public async Task<WorkspaceUpsertPersistenceResult> UpsertPathAsync(
        Guid depotId, string normalizedPath, string name, string? description, string metadataJson,
        bool createMissingParents, DateTimeOffset now, CancellationToken cancellationToken)
    {
        try
        {
            var plan = await new WorkspaceWriter(this, idGenerator, workspaceAccess).UpsertAsync(depotId,
                new UpsertWorkspaceCommand(normalizedPath, name, description, metadataJson, createMissingParents), now, cancellationToken);
            return new(plan.Target.Workspace, plan.Outcome);
        }
        catch (ContextDepot.Application.Shared.Exceptions.ContextDepotApplicationException error)
            when (error.ErrorCode == ContextDepot.Application.Shared.Exceptions.ApplicationErrorCodes.WorkspaceParentNotFound ||
                  error.ErrorCode == ContextDepot.Application.Shared.Exceptions.ApplicationErrorCodes.WorkspaceConcurrencyConflict)
        {
            return new(null, error.ErrorCode == ContextDepot.Application.Shared.Exceptions.ApplicationErrorCodes.WorkspaceParentNotFound
                ? WorkspaceUpsertPersistenceOutcome.ParentNotFound : WorkspaceUpsertPersistenceOutcome.ConcurrencyConflict);
        }
    }
}
