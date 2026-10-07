using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.CurrentDepot;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ContextDepot.Infrastructure.Tests;

public sealed class WorkspaceAccessGrantQueryFilterTests
{
    [Fact]
    public void ModelHasConsistentRequiredNavigationQueryFilters()
    {
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
            .UseNpgsql("Host=127.0.0.1;Database=model_validation")
            .ConfigureWarnings(warnings => warnings.Throw(CoreEventId.PossibleIncorrectRequiredNavigationWithQueryFilterInteractionWarning))
            .Options;
        using var db = new ContextDepotDbContext(options, new CurrentDepotAccessContext());
        Assert.NotNull(db.Model.FindEntityType(typeof(WorkspaceAccessGrant)));
    }

    [PostgreSqlFact]
    public async Task GrantsMatchNavigableWorkspacesWhileAuthenticationCanResolveAnUninitializedScope()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        var now = DateTimeOffset.UtcNow;
        var depot = new Depot(Guid.CreateVersion7(), "Scoped depot", now);
        var root = new Workspace(Guid.CreateVersion7(), depot.Id, null, "Root", "root", null, now);
        var child = new Workspace(Guid.CreateVersion7(), depot.Id, root.Id, "Child", "child", null, now);
        var hidden = new Workspace(Guid.CreateVersion7(), depot.Id, null, "Hidden", "hidden", null, now);
        var hasher = new DepotAccessKeySecretHasher();
        var secret = hasher.Generate();
        var key = new DepotAccessKey(Guid.CreateVersion7(), depot.Id, "Scoped client", secret.Prefix, secret.SecretHash, now);
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, depot.Id, root.Id, now));
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, depot.Id, child.Id, now));
        key.WorkspaceGrants.Add(new WorkspaceAccessGrant(key.Id, depot.Id, hidden.Id, now));
        await using (var seed = fixture.CreateDb(true))
        {
            seed.AddRange(depot, root, child, hidden, key);
            await seed.SaveChangesAsync();
        }
        await using (var uninitialized = fixture.CreateDb())
        {
            Assert.Empty(await uninitialized.WorkspaceAccessGrants.ToArrayAsync());
            var identity = await new DepotAccessKeyAuthenticator(uninitialized, hasher, TimeProvider.System).AuthenticateAsync(secret.Plaintext);
            Assert.NotNull(identity);
            Assert.Equal(3, identity.WorkspaceIds.Count);
        }
        var access = new CurrentDepotAccessContext();
        access.Initialize(new DepotAccessKeyIdentity(key.Id, depot.Id, depot.DisplayName, [child.Id], [root.Id, child.Id]));
        await using var scoped = fixture.CreateDb(access);
        var grants = await scoped.WorkspaceAccessGrants.AsNoTracking().OrderBy(item => item.WorkspaceId).ToArrayAsync();
        var joined = await scoped.WorkspaceAccessGrants.AsNoTracking().Include(item => item.Workspace).OrderBy(item => item.WorkspaceId).ToArrayAsync();
        Assert.Equal(2, grants.Length);
        Assert.Equal(grants.Select(item => item.WorkspaceId), joined.Select(item => item.WorkspaceId));
        Assert.DoesNotContain(grants, item => item.WorkspaceId == hidden.Id);
        Assert.All(joined, item => Assert.NotNull(item.Workspace));
    }
}
