using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

public sealed class AccessKeyRepositoryTests
{
    [PostgreSqlFact]
    public async Task KeysStayInTheirDepotAndRotationRevokesOldSecretAndPreservesGrants()
    {
        var access = new CurrentDepotAccessContext(); access.AllowInternalAccess();
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>().UseNpgsql(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")).Options;
        await using var db = new ContextDepotDbContext(options, access);
        var now = DateTimeOffset.UtcNow;
        var depot = new Depot(Guid.CreateVersion7(), "Key integration", now);
        var other = new Depot(Guid.CreateVersion7(), "Other integration", now);
        var root = new Workspace(Guid.CreateVersion7(), depot.Id, null, "Root", "root", null, now);
        var child = new Workspace(Guid.CreateVersion7(), depot.Id, root.Id, "Child", "child", null, now);
        var foreign = new Workspace(Guid.CreateVersion7(), other.Id, null, "Other", "other", null, now);
        db.AddRange(depot, other, root, child, foreign); await db.SaveChangesAsync();
        try
        {
            var hasher = new DepotAccessKeySecretHasher();
            var repository = new AccessKeyRepository(db);
            var original = hasher.Generate();
            var key = new DepotAccessKey(Guid.CreateVersion7(), depot.Id, "Client", original.Prefix, original.SecretHash, now);
            key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, depot.Id, root.Id, now));
            await repository.CreateAsync(key, default);
            Assert.False(await repository.WorkspacesBelongToDepotAsync(depot.Id, [foreign.Id], default));
            Assert.Empty((await repository.ListAsync(other.Id, default)).Items);
            Assert.Null(await repository.RevokeAsync(other.Id, key.Id, now, default));
            var authenticator = new DepotAccessKeyAuthenticator(db, hasher, TimeProvider.System);
            var identity = await authenticator.AuthenticateAsync(original.Plaintext);
            Assert.Equal(new[] { root.Id }, identity!.WorkspaceIds);
            Assert.DoesNotContain(child.Id, identity.WorkspaceIds);
            await repository.SetGrantsAsync(depot.Id, key.Id, [child.Id], now, default);
            db.ChangeTracker.Clear();
            identity = await authenticator.AuthenticateAsync(original.Plaintext);
            Assert.Equal(new[] { child.Id }, identity!.WorkspaceIds);
            Assert.Contains(root.Id, identity.NavigableWorkspaceIds);
            var replacement = hasher.Generate();
            var rotated = await repository.RotateAsync(depot.Id, key.Id, replacement, Guid.CreateVersion7(), now, default);
            Assert.Equal(new[] { child.Id }, rotated!.WorkspaceIds);
            db.ChangeTracker.Clear();
            Assert.Null(await authenticator.AuthenticateAsync(original.Plaintext));
            Assert.NotNull(await authenticator.AuthenticateAsync(replacement.Plaintext));
            await repository.RevokeAsync(depot.Id, rotated.Id, now, default);
            db.ChangeTracker.Clear();
            Assert.Null(await authenticator.AuthenticateAsync(replacement.Plaintext));
            var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => repository.RotateAsync(depot.Id, rotated.Id, hasher.Generate(), Guid.CreateVersion7(), now, default));
            Assert.Equal(ApplicationErrorCodes.SettingsConflict, error.ErrorCode);
            Assert.DoesNotContain("SecretHash", (await repository.ListAsync(depot.Id, default)).Items[0].ToString());
        }
        finally
        {
            db.ChangeTracker.Clear();
            await db.WorkspaceAccessGrants.Where(grant => grant.DepotId == depot.Id || grant.DepotId == other.Id).ExecuteDeleteAsync();
            await db.DepotAccessKeys.Where(key => key.DepotId == depot.Id || key.DepotId == other.Id).ExecuteDeleteAsync();
            await db.Workspaces.Where(workspace => workspace.Id == child.Id).ExecuteDeleteAsync();
            await db.Workspaces.Where(workspace => workspace.DepotId == depot.Id || workspace.DepotId == other.Id).ExecuteDeleteAsync();
            await db.Depots.Where(item => item.Id == depot.Id || item.Id == other.Id).ExecuteDeleteAsync();
        }
    }
}
