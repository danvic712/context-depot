using ContextDepot.Infrastructure.Contracts;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.CurrentDepot;

public sealed class DepotAccessKeyAuthenticator(
    ContextDepotDbContext db,
    DepotAccessKeySecretHasher secretHasher,
    TimeProvider timeProvider) : IDepotAccessKeyAuthenticator
{
    public async Task<DepotAccessKeyIdentity?> AuthenticateAsync(
        string presentedKey,
        CancellationToken cancellationToken = default)
    {
        if (!secretHasher.TryGetPrefix(presentedKey, out var prefix))
        {
            return null;
        }

        var accessKey = await db.DepotAccessKeys
            .Include(key => key.Depot)
            .Include(key => key.WorkspaceGrants)
            .SingleOrDefaultAsync(
                key => key.KeyPrefix == prefix && key.RevokedAt == null,
                cancellationToken);
        if (accessKey?.Depot is null || !secretHasher.Verify(presentedKey, accessKey.SecretHash))
        {
            return null;
        }

        var workspaceIds = accessKey.WorkspaceGrants
            .Select(grant => grant.WorkspaceId)
            .Distinct()
            .ToArray();
        var workspaceTree = await db.Workspaces
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Where(workspace => workspace.DepotId == accessKey.DepotId)
            .Select(workspace => new WorkspaceNode(workspace.Id, workspace.ParentWorkspaceId))
            .ToListAsync(cancellationToken);
        var navigableWorkspaceIds = ResolveNavigableWorkspaceIds(workspaceIds, workspaceTree);

        accessKey.MarkUsed(timeProvider.GetUtcNow());
        await db.SaveChangesAsync(cancellationToken);
        return new DepotAccessKeyIdentity(
            accessKey.Id,
            accessKey.DepotId,
            accessKey.Depot.DisplayName,
            workspaceIds,
            navigableWorkspaceIds);
    }

    private static Guid[] ResolveNavigableWorkspaceIds(
        IReadOnlyCollection<Guid> workspaceIds,
        IReadOnlyCollection<WorkspaceNode> workspaceTree)
    {
        var parents = workspaceTree.ToDictionary(workspace => workspace.Id, workspace => workspace.ParentWorkspaceId);
        var navigable = new HashSet<Guid>(workspaceIds);
        foreach (var workspaceId in workspaceIds)
        {
            var currentId = workspaceId;
            var visited = new HashSet<Guid>();
            while (visited.Add(currentId) && parents.TryGetValue(currentId, out var parentId) && parentId is Guid parent)
            {
                navigable.Add(parent);
                currentId = parent;
            }
        }

        return navigable.Order().ToArray();
    }

    private sealed record WorkspaceNode(Guid Id, Guid? ParentWorkspaceId);
}
