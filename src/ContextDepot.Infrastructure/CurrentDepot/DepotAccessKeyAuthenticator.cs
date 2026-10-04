using ContextDepot.Infrastructure.Contracts;
using ContextDepot.Application.Workspaces;
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
            .AsNoTracking()
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
            .Select(workspace => new WorkspaceTreeNode(workspace.Id, workspace.ParentWorkspaceId, workspace.Slug))
            .ToListAsync(cancellationToken);
        var navigableWorkspaceIds = new WorkspaceTopology(workspaceTree)
            .AncestorsIncludingSelf(workspaceIds)
            .Order()
            .ToArray();

        var now = timeProvider.GetUtcNow();
        var updateBefore = now.AddMinutes(-1);
        if (accessKey.LastUsedAt is null || accessKey.LastUsedAt < updateBefore)
        {
            await db.DepotAccessKeys.Where(key => key.Id == accessKey.Id && key.RevokedAt == null &&
                    (key.LastUsedAt == null || key.LastUsedAt < updateBefore))
                .ExecuteUpdateAsync(update => update.SetProperty(key => key.LastUsedAt, now), cancellationToken);
        }
        return new DepotAccessKeyIdentity(
            accessKey.Id,
            accessKey.DepotId,
            accessKey.Depot.DisplayName,
            workspaceIds,
            navigableWorkspaceIds);
    }

}
