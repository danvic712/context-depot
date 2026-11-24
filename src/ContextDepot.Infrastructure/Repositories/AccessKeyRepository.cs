using System.Globalization;
using ContextDepot.Application.Settings;
using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Access;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class AccessKeyRepository(ContextDepotDbContext db) : IAccessKeyRepository
{
    public async Task<AccessKeyListDto> ListAsync(CancellationToken cancellationToken)
    {
        var keys = await db.AccessKeys.AsNoTracking()
            .Include(key => key.WorkspaceGrants).OrderByDescending(key => key.CreatedAt).ThenBy(key => key.Id)
            .ToListAsync(cancellationToken);
        var workspaces = await db.Workspaces.AsNoTracking()
            .ToListAsync(cancellationToken);
        var paths = WorkspacePath.BuildPaths(workspaces);
        return new AccessKeyListDto(keys.Select(ToDto).ToArray(), workspaces
            .Select(workspace => new AccessKeyWorkspaceDto(workspace.Id, workspace.Name, paths[workspace.Id]))
            .OrderBy(workspace => workspace.Path, StringComparer.Ordinal).ToArray());
    }

    public async Task<bool> WorkspacesExistAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await db.Workspaces.CountAsync(workspace => ids.Contains(workspace.Id), cancellationToken) == ids.Count;

    public async Task<AccessKeyDto> CreateAsync(AccessKey key, CancellationToken cancellationToken)
    {
        db.AccessKeys.Add(key);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(key);
    }

    public async Task<AccessKeyDto?> RevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = await LockAsync(id, cancellationToken);
        if (key is null) return null;
        key.Revoke(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(key);
    }

    public async Task<AccessKeyDto?> RotateAsync(Guid id, GeneratedAccessKey secret, Guid replacementId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = await LockAsync(id, cancellationToken);
        if (key is null) return null;
        if (!key.IsActive) throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
        var replacement = new AccessKey(replacementId, key.Name, secret.Prefix, secret.SecretHash, now,
            key.CanCreateRootWorkspaces);
        foreach (var grant in key.WorkspaceGrants)
            replacement.WorkspaceGrants.Add(new WorkspaceAccessGrant(replacement.Id, grant.WorkspaceId, now));
        db.AccessKeys.Add(replacement);
        // Rotation replaces the credential rather than accumulating revoked keys and their grants.
        db.WorkspaceAccessGrants.RemoveRange(key.WorkspaceGrants);
        db.AccessKeys.Remove(key);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(replacement);
    }

    public Task<AccessKeyDto?> SetGrantsAsync(Guid id, IReadOnlyList<Guid> ids,
        DateTimeOffset now, CancellationToken cancellationToken) => SetAccessAsync(id,
            new(ids, null, null), now, cancellationToken);

    public async Task<AccessKeyDto?> SetAccessAsync(Guid id, AccessKeyAccessUpdate command,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = await LockAsync(id, cancellationToken);
        if (key is null) return null;
        if (!key.IsActive) throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
        var capability = AccessKeyAccessPolicy.Approve(key, command);
        var ids = command.WorkspaceIds;
        var changed = capability != key.CanCreateRootWorkspaces || !key.WorkspaceGrants.Select(grant => grant.WorkspaceId).Order().SequenceEqual(ids.Order());
        var removed = key.WorkspaceGrants.Where(grant => !ids.Contains(grant.WorkspaceId)).ToArray();
        foreach (var grant in removed)
        {
            key.WorkspaceGrants.Remove(grant);
            db.WorkspaceAccessGrants.Remove(grant);
        }
        var existing = key.WorkspaceGrants.Select(grant => grant.WorkspaceId).ToHashSet();
        foreach (var workspaceId in ids.Where(workspaceId => !existing.Contains(workspaceId)))
            key.WorkspaceGrants.Add(new WorkspaceAccessGrant(id, workspaceId, now));
        key.ChangeCreationCapability(capability);
        if (changed) key.AdvanceAccessVersion();
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(key);
    }

    private async Task<AccessKey?> LockAsync(Guid id, CancellationToken cancellationToken)
    {
        var keys = await db.AccessKeys.FromSqlInterpolated(
            $"SELECT * FROM public.access_keys WHERE id = {id} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var key = keys.SingleOrDefault();
        if (key is not null)
        {
            await db.Entry(key).ReloadAsync(cancellationToken);
            foreach (var grant in key.WorkspaceGrants.ToArray()) db.Entry(grant).State = EntityState.Detached;
            key.WorkspaceGrants.Clear();
            await db.Entry(key).Collection(item => item.WorkspaceGrants).Query().IgnoreQueryFilters().LoadAsync(cancellationToken);
        }
        return key;
    }

    private static AccessKeyDto ToDto(AccessKey key) => new(key.Id, key.Name, key.KeyPrefix,
        key.CreatedAt, key.LastUsedAt, key.RevokedAt, key.WorkspaceGrants.Select(grant => grant.WorkspaceId).Order().ToArray(),
        key.CanCreateRootWorkspaces, key.AccessVersion.ToString(CultureInfo.InvariantCulture));
}
