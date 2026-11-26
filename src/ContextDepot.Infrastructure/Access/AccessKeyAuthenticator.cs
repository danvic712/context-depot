using ContextDepot.Infrastructure.Contracts;
using ContextDepot.Application.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Access;

public sealed class AccessKeyAuthenticator(
    ContextDepotDbContext db,
    AccessKeySecretHasher secretHasher,
    TimeProvider timeProvider) : IAccessKeyAuthenticator
{
    public async Task<AccessKeyIdentity?> AuthenticateAsync(
        string presentedKey,
        CancellationToken cancellationToken = default)
    {
        if (!secretHasher.TryGetPrefix(presentedKey, out var prefix))
        {
            return null;
        }

        var accessKey = await db.AccessKeys
            // Authentication resolves the access scope before scoped filters can be applied.
            .IgnoreQueryFilters()
            .AsNoTracking()
            .Include(key => key.WorkspaceGrants)
            .SingleOrDefaultAsync(
                key => key.KeyPrefix == prefix && key.RevokedAt == null,
                cancellationToken);
        if (accessKey is null || !secretHasher.Verify(presentedKey, accessKey.SecretHash))
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
            await db.AccessKeys.Where(key => key.Id == accessKey.Id && key.RevokedAt == null &&
                    (key.LastUsedAt == null || key.LastUsedAt < updateBefore))
                .ExecuteUpdateAsync(update => update.SetProperty(key => key.LastUsedAt, now), cancellationToken);
        }
        return new AccessKeyIdentity(
            accessKey.Id,
            workspaceIds,
            navigableWorkspaceIds,
            accessKey.CanCreateRootWorkspaces,
            accessKey.AccessVersion);
    }

}
