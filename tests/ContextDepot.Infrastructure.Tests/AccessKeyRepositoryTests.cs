using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Access;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.Access;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

public sealed class AccessKeyRepositoryTests
{
    [PostgreSqlFact]
    public async Task RotationReplacesOldRecordAndPreservesGrants()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(true);
        var now = DateTimeOffset.UtcNow;


        var root = new Workspace(Guid.CreateVersion7(), null, "Root", "root", null, now);
        var child = new Workspace(Guid.CreateVersion7(), root.Id, "Child", "child", null, now);
        var foreign = new Workspace(Guid.CreateVersion7(), null, "Other", "other", null, now);
        db.AddRange(root, child, foreign); await db.SaveChangesAsync();
        var hasher = new AccessKeySecretHasher();
        var repository = new AccessKeyRepository(db);
        var original = hasher.Generate();
        var key = new AccessKey(Guid.CreateVersion7(), "Client", original.Prefix, original.SecretHash, now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, root.Id, now));
        await repository.CreateAsync(key, default);
        Assert.False(await repository.WorkspacesExistAsync([Guid.NewGuid()], default));
        var authenticator = new AccessKeyAuthenticator(db, hasher, TimeProvider.System);
        var identity = await authenticator.AuthenticateAsync(original.Plaintext);
        Assert.Equal(new[] { root.Id }, identity!.WorkspaceIds);
        Assert.DoesNotContain(foreign.Id, identity.NavigableWorkspaceIds);
        Assert.DoesNotContain(child.Id, identity.WorkspaceIds);
        await repository.SetGrantsAsync(key.Id, [child.Id], now, default);
        db.ChangeTracker.Clear();
        identity = await authenticator.AuthenticateAsync(original.Plaintext);
        Assert.Equal(new[] { child.Id }, identity!.WorkspaceIds);
        Assert.Contains(root.Id, identity.NavigableWorkspaceIds);
        var replacement = hasher.Generate();
        var rotated = await repository.RotateAsync(key.Id, replacement, Guid.CreateVersion7(), now, default);
        Assert.Equal(new[] { child.Id }, rotated!.WorkspaceIds);
        db.ChangeTracker.Clear();
        Assert.Null(await authenticator.AuthenticateAsync(original.Plaintext));
        Assert.NotNull(await authenticator.AuthenticateAsync(replacement.Plaintext));
        Assert.False(await db.AccessKeys.AnyAsync(item => item.Id == key.Id));
        Assert.False(await db.WorkspaceAccessGrants.AnyAsync(grant => grant.AccessKeyId == key.Id));
        Assert.Equal(rotated.Id, Assert.Single((await repository.ListAsync(default)).Items).Id);
        Assert.Null(await repository.RotateAsync(Guid.NewGuid(), hasher.Generate(), Guid.CreateVersion7(), now, default));
        var nextSecret = hasher.Generate();
        var next = await repository.RotateAsync(rotated.Id, nextSecret, Guid.CreateVersion7(), now, default);
        Assert.Equal("Client", next!.Name);
        Assert.Equal(new[] { child.Id }, next.WorkspaceIds);
        db.ChangeTracker.Clear();
        Assert.Null(await authenticator.AuthenticateAsync(replacement.Plaintext));
        Assert.NotNull(await authenticator.AuthenticateAsync(nextSecret.Plaintext));
        Assert.Equal(next.Id, Assert.Single((await repository.ListAsync(default)).Items).Id);
        Assert.False(await db.WorkspaceAccessGrants.AnyAsync(grant => grant.AccessKeyId == rotated.Id));
        await repository.RevokeAsync(next.Id, now, default);
        db.ChangeTracker.Clear();
        Assert.Null(await authenticator.AuthenticateAsync(nextSecret.Plaintext));
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => repository.RotateAsync(next.Id, hasher.Generate(), Guid.CreateVersion7(), now, default));
        Assert.Equal(ApplicationErrorCodes.SettingsConflict, error.ErrorCode);
        Assert.DoesNotContain("SecretHash", (await repository.ListAsync(default)).Items[0].ToString());
    }

    [PostgreSqlFact]
    public async Task FailedReplacementPreservesTheOriginalKeyAndGrants()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;

        var workspace = new Workspace(Guid.CreateVersion7(), null, "Root", "root", null, now);
        var hasher = new AccessKeySecretHasher();
        var original = hasher.Generate();
        var occupied = hasher.Generate();
        var key = new AccessKey(Guid.CreateVersion7(), "Client", original.Prefix, original.SecretHash, now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, workspace.Id, now));
        var otherKey = new AccessKey(Guid.CreateVersion7(), "Other client", occupied.Prefix, occupied.SecretHash, now);
        await using (var db = fixture.CreateDb(unrestricted: true))
        {
            db.AddRange(workspace, key, otherKey);
            await db.SaveChangesAsync();
            var repository = new AccessKeyRepository(db);
            // A colliding prefix forces a database failure after the replacement transaction begins.
            await Assert.ThrowsAsync<DbUpdateException>(() => repository.RotateAsync(
                key.Id, occupied, Guid.CreateVersion7(), now, default));
        }
        await using var verify = fixture.CreateDb(unrestricted: true);
        Assert.Equal(2, await verify.AccessKeys.CountAsync());
        Assert.Equal(workspace.Id, (await verify.WorkspaceAccessGrants.SingleAsync()).WorkspaceId);
        var identity = await new AccessKeyAuthenticator(verify, hasher, TimeProvider.System).AuthenticateAsync(original.Plaintext);
        Assert.Equal(key.Id, identity!.AccessKeyId);
        Assert.Equal(new[] { workspace.Id }, identity.WorkspaceIds);
        Assert.Null((await verify.AccessKeys.SingleAsync(item => item.Id == key.Id)).RevokedAt);
    }
}
