using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class AccessKeyRepository(ContextDepotDbContext db) : IAccessKeyRepository
{
    public async Task<AccessKeyListDto> ListAsync(Guid depotId, CancellationToken cancellationToken)
    {
        var keys = await db.DepotAccessKeys.AsNoTracking().Include(key => key.WorkspaceGrants)
            .Where(key => key.DepotId == depotId).OrderByDescending(key => key.CreatedAt).ThenBy(key => key.Id)
            .ToListAsync(cancellationToken);
        var workspaces = await db.Workspaces.AsNoTracking().Where(workspace => workspace.DepotId == depotId)
            .ToListAsync(cancellationToken);
        var paths = WorkspacePath.BuildPaths(workspaces);
        return new AccessKeyListDto(keys.Select(ToDto).ToArray(), workspaces
            .Select(workspace => new AccessKeyWorkspaceDto(workspace.Id, workspace.Name, paths[workspace.Id]))
            .OrderBy(workspace => workspace.Path, StringComparer.Ordinal).ToArray());
    }

    public async Task<bool> WorkspacesBelongToDepotAsync(Guid depotId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken) =>
        await db.Workspaces.CountAsync(workspace => workspace.DepotId == depotId && ids.Contains(workspace.Id), cancellationToken) == ids.Count;

    public async Task<AccessKeyDto> CreateAsync(DepotAccessKey key, CancellationToken cancellationToken)
    {
        db.DepotAccessKeys.Add(key);
        await db.SaveChangesAsync(cancellationToken);
        return ToDto(key);
    }

    public async Task<AccessKeyDto?> RevokeAsync(Guid depotId, Guid id, DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = await LockAsync(depotId, id, cancellationToken);
        if (key is null) return null;
        key.Revoke(now);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(key);
    }

    public async Task<AccessKeyDto?> RotateAsync(Guid depotId, Guid id, GeneratedAccessKey secret, Guid replacementId,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = await LockAsync(depotId, id, cancellationToken);
        if (key is null) return null;
        if (!key.IsActive) throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
        var replacement = new DepotAccessKey(replacementId, depotId, key.Name, secret.Prefix, secret.SecretHash, now);
        foreach (var grant in key.WorkspaceGrants)
            replacement.WorkspaceGrants.Add(new WorkspaceAccessGrant(replacement.Id, depotId, grant.WorkspaceId, now));
        key.Revoke(now);
        db.DepotAccessKeys.Add(replacement);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(replacement);
    }

    public async Task<AccessKeyDto?> SetGrantsAsync(Guid depotId, Guid id, IReadOnlyList<Guid> ids,
        DateTimeOffset now, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var key = await LockAsync(depotId, id, cancellationToken);
        if (key is null) return null;
        if (!key.IsActive) throw new ContextDepotApplicationException(ApplicationErrorCodes.SettingsConflict);
        var removed = key.WorkspaceGrants.Where(grant => !ids.Contains(grant.WorkspaceId)).ToArray();
        foreach (var grant in removed)
        {
            key.WorkspaceGrants.Remove(grant);
            db.WorkspaceAccessGrants.Remove(grant);
        }
        var existing = key.WorkspaceGrants.Select(grant => grant.WorkspaceId).ToHashSet();
        foreach (var workspaceId in ids.Where(workspaceId => !existing.Contains(workspaceId)))
            key.WorkspaceGrants.Add(new WorkspaceAccessGrant(id, depotId, workspaceId, now));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return ToDto(key);
    }

    private async Task<DepotAccessKey?> LockAsync(Guid depotId, Guid id, CancellationToken cancellationToken)
    {
        var keys = await db.DepotAccessKeys.FromSqlInterpolated(
            $"SELECT * FROM public.depot_access_keys WHERE id = {id} AND depot_id = {depotId} FOR UPDATE")
            .ToListAsync(cancellationToken);
        var key = keys.SingleOrDefault();
        if (key is not null) await db.Entry(key).Collection(item => item.WorkspaceGrants).LoadAsync(cancellationToken);
        return key;
    }

    private static AccessKeyDto ToDto(DepotAccessKey key) => new(key.Id, key.Name, key.KeyPrefix,
        key.CreatedAt, key.LastUsedAt, key.RevokedAt, key.WorkspaceGrants.Select(grant => grant.WorkspaceId).Order().ToArray());
}
