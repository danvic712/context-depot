using System.Data;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class WorkspaceRepository(
    ContextDepotDbContext db,
    IIdGenerator idGenerator,
    IWorkspaceAccessContext workspaceAccess) : IWorkspaceRepository
{
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

        var workspaces = await db.Workspaces.AsNoTracking().Where(x => x.DepotId == depotId).ToListAsync(cancellationToken);
        var workspace = workspaces.SingleOrDefault(x => x.Id == workspaceId);
        if (workspace is null)
        {
            return null;
        }

        var paths = WorkspacePath.BuildPaths(workspaces);
        return new WorkspacePathLookup(workspace, paths[workspace.Id]);
    }

    public async Task<Workspace?> GetByPathAsync(Guid depotId, string normalizedPath, CancellationToken cancellationToken)
    {
        var workspaces = await db.Workspaces.AsNoTracking().Where(x => x.DepotId == depotId).ToListAsync(cancellationToken);
        var workspace = FindByPath(workspaces, normalizedPath);
        return workspace is not null && workspaceAccess.CanAccess(workspace.Id) ? workspace : null;
    }

    public async Task<IReadOnlyList<WorkspacePathLookup>> ListWithPathsAsync(Guid depotId, string? parentPath, CancellationToken cancellationToken)
    {
        var workspaces = await db.Workspaces.AsNoTracking().Where(x => x.DepotId == depotId).ToListAsync(cancellationToken);
        var paths = WorkspacePath.BuildPaths(workspaces);
        var byPath = paths.ToDictionary(x => x.Value, x => x.Key, StringComparer.Ordinal);
        Guid? parentId = null;
        if (!string.IsNullOrWhiteSpace(parentPath))
        {
            parentId = byPath.GetValueOrDefault(parentPath);
            if (parentId is null)
            {
                return [];
            }
        }

        return workspaces
            .Where(x => x.ParentWorkspaceId == parentId && workspaceAccess.CanAccess(x.Id))
            .OrderBy(x => x.Slug)
            .Select(x => new WorkspacePathLookup(x, paths[x.Id]))
            .ToArray();
    }

    public async Task<WorkspaceUpsertPersistenceResult> UpsertPathAsync(
        Guid depotId,
        string normalizedPath,
        string name,
        string? description,
        string metadataJson,
        bool createMissingParents,
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var workspaces = await db.Workspaces.Where(x => x.DepotId == depotId).ToListAsync(cancellationToken);
            var paths = WorkspacePath.BuildPaths(workspaces);
            var byId = workspaces.ToDictionary(x => x.Id);
            var byPath = paths.ToDictionary(x => x.Value, x => byId[x.Key], StringComparer.Ordinal);
            if (byPath.TryGetValue(normalizedPath, out var existing))
            {
                if (!workspaceAccess.CanAccess(existing.Id))
                {
                    return new WorkspaceUpsertPersistenceResult(null, WorkspaceUpsertPersistenceOutcome.ParentNotFound);
                }

                if (string.Equals(existing.Name, name, StringComparison.Ordinal) &&
                    string.Equals(existing.Description, description, StringComparison.Ordinal) &&
                    string.Equals(existing.MetadataJson, metadataJson, StringComparison.Ordinal))
                {
                    return new WorkspaceUpsertPersistenceResult(existing, WorkspaceUpsertPersistenceOutcome.ReusedExisting);
                }

                existing.Update(name, description, metadataJson, now);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return new WorkspaceUpsertPersistenceResult(existing, WorkspaceUpsertPersistenceOutcome.Updated);
            }

            Guid? parentId = null;
            var currentPath = string.Empty;
            var segments = normalizedPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            Workspace? result = null;
            foreach (var segment in segments)
            {
                currentPath = currentPath.Length == 0 ? segment : currentPath + "/" + segment;
                if (byPath.TryGetValue(currentPath, out var current))
                {
                    parentId = current.Id;
                    result = current;
                    continue;
                }

                var isLeaf = currentPath == normalizedPath;
                if (!isLeaf && !createMissingParents)
                {
                    return new WorkspaceUpsertPersistenceResult(null, WorkspaceUpsertPersistenceOutcome.ParentNotFound);
                }

                if (!workspaceAccess.HasUnrestrictedAccess &&
                    (parentId is null || !workspaceAccess.CanAccess(parentId.Value)))
                {
                    return new WorkspaceUpsertPersistenceResult(null, WorkspaceUpsertPersistenceOutcome.ParentNotFound);
                }

                var workspace = new Workspace(
                    idGenerator.NewId(),
                    depotId,
                    parentId,
                    isLeaf ? name : segment,
                    segment,
                    isLeaf ? description : null,
                    now);
                workspace.Update(isLeaf ? name : segment, isLeaf ? description : null, isLeaf ? metadataJson : "{}", now);
                db.Workspaces.Add(workspace);
                if (workspaceAccess.DepotAccessKeyId is Guid depotAccessKeyId)
                {
                    db.WorkspaceAccessGrants.Add(new WorkspaceAccessGrant(
                        depotAccessKeyId,
                        depotId,
                        workspace.Id,
                        now));
                }

                workspaces.Add(workspace);
                byPath[currentPath] = workspace;
                parentId = workspace.Id;
                result = workspace;
            }

            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return new WorkspaceUpsertPersistenceResult(result, WorkspaceUpsertPersistenceOutcome.Created);
        }
        catch (DbUpdateException)
        {
            await transaction.RollbackAsync(cancellationToken);
            return new WorkspaceUpsertPersistenceResult(null, WorkspaceUpsertPersistenceOutcome.ConcurrencyConflict);
        }
    }

    private static Workspace? FindByPath(IReadOnlyList<Workspace> workspaces, string path)
    {
        var paths = WorkspacePath.BuildPaths(workspaces);
        var workspaceById = workspaces.ToDictionary(x => x.Id);
        foreach (var (workspaceId, workspacePath) in paths)
        {
            if (string.Equals(workspacePath, path, StringComparison.Ordinal))
            {
                return workspaceById[workspaceId];
            }
        }

        return null;
    }
}
