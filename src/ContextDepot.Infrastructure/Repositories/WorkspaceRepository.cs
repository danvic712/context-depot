using System.Data;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class WorkspaceRepository(ContextDepotDbContext db, IIdGenerator idGenerator) : IWorkspaceRepository
{
    public Task<Workspace?> GetByIdAsync(Guid ownerId, Guid workspaceId, CancellationToken cancellationToken) =>
        db.Workspaces.AsNoTracking().SingleOrDefaultAsync(x => x.OwnerId == ownerId && x.Id == workspaceId, cancellationToken);

    public async Task<WorkspacePathLookup?> GetByIdWithPathAsync(Guid ownerId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspaces = await db.Workspaces.AsNoTracking().Where(x => x.OwnerId == ownerId).ToListAsync(cancellationToken);
        var workspace = workspaces.SingleOrDefault(x => x.Id == workspaceId);
        return workspace is null ? null : new WorkspacePathLookup(workspace, BuildPath(workspaces, workspace));
    }

    public async Task<Workspace?> GetByPathAsync(Guid ownerId, string normalizedPath, CancellationToken cancellationToken)
    {
        var workspaces = await db.Workspaces.AsNoTracking().Where(x => x.OwnerId == ownerId).ToListAsync(cancellationToken);
        return FindByPath(workspaces, normalizedPath);
    }

    public async Task<IReadOnlyList<WorkspacePathLookup>> ListWithPathsAsync(Guid ownerId, string? parentPath, CancellationToken cancellationToken)
    {
        var workspaces = await db.Workspaces.AsNoTracking().Where(x => x.OwnerId == ownerId).ToListAsync(cancellationToken);
        Guid? parentId = null;
        if (!string.IsNullOrWhiteSpace(parentPath))
        {
            parentId = FindByPath(workspaces, parentPath)?.Id;
            if (parentId is null)
            {
                return [];
            }
        }

        return workspaces
            .Where(x => x.ParentWorkspaceId == parentId)
            .OrderBy(x => x.Slug)
            .Select(x => new WorkspacePathLookup(x, BuildPath(workspaces, x)))
            .ToArray();
    }

    public async Task<WorkspaceUpsertPersistenceResult> UpsertPathAsync(
        Guid ownerId,
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
            var workspaces = await db.Workspaces.Where(x => x.OwnerId == ownerId).ToListAsync(cancellationToken);
            var byPath = workspaces.ToDictionary(x => BuildPath(workspaces, x), StringComparer.Ordinal);
            if (byPath.TryGetValue(normalizedPath, out var existing))
            {
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

                var workspace = new Workspace(
                    idGenerator.NewId(),
                    ownerId,
                    parentId,
                    isLeaf ? name : segment,
                    segment,
                    isLeaf ? description : null,
                    now);
                workspace.Update(isLeaf ? name : segment, isLeaf ? description : null, isLeaf ? metadataJson : "{}", now);
                db.Workspaces.Add(workspace);
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
        var byPath = workspaces.ToDictionary(x => BuildPath(workspaces, x), StringComparer.Ordinal);
        return byPath.GetValueOrDefault(path);
    }

    private static string BuildPath(IReadOnlyList<Workspace> workspaces, Workspace workspace)
    {
        var byId = workspaces.ToDictionary(x => x.Id);
        var segments = new Stack<string>();
        var current = workspace;
        var visited = new HashSet<Guid>();
        while (visited.Add(current.Id))
        {
            segments.Push(current.Slug);
            if (current.ParentWorkspaceId is not Guid parentId || !byId.TryGetValue(parentId, out current!))
            {
                break;
            }
        }

        return string.Join('/', segments);
    }
}
