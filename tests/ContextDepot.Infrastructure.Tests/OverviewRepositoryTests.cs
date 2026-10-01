using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")))
            Skip = "Set CONTEXTDEPOT_TEST_CONNECTION for PostgreSQL integration tests. All writes are rolled back.";
    }
}

public sealed class OverviewRepositoryTests
{
    [PostgreSqlFact]
    public async Task CollectionsRespectLifecycleDirectCountsPathsDepotAndStableOrdering()
    {
        var access = new CurrentDepotAccessContext();
        access.AllowInternalAccess();
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")).Options;
        await using var db = new ContextDepotDbContext(options, access);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.Parse("2026-10-01T00:00:00Z");
        var depot = new Depot(Guid.NewGuid(), "Integration test", now);
        var otherDepot = new Depot(Guid.NewGuid(), "Other test", now);
        var root = new Workspace(Guid.NewGuid(), depot.Id, null, "Root", "root", null, now.AddDays(-2));
        var child = new Workspace(Guid.NewGuid(), depot.Id, root.Id, "Child", "child", null, now.AddDays(-2));
        var empty = new Workspace(Guid.NewGuid(), depot.Id, null, "Empty", "empty", null, now.AddDays(-3));
        var other = new Workspace(Guid.NewGuid(), otherDepot.Id, null, "Other", "other", null, now);
        db.AddRange(depot, otherDepot, root, child, empty, other);
        var active = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Decision, null, "", "**A decision**\n\nDetails", now.AddHours(-2));
        var expired = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Fact, null, "Expired", "Expired", now);
        expired.SetValidity(null, null, now);
        var future = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Fact, null, "Future", "Future", now);
        future.SetValidity(now.AddDays(1), null, null);
        var superseded = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Fact, null, "Old", "Old", now);
        superseded.MarkSuperseded(now);
        var archivedContext = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Fact, null, "Archived", "Archived", now);
        archivedContext.MarkArchived(now);
        var ended = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Fact, null, "Ended", "Ended", now);
        ended.SetValidity(now.AddDays(-1), now, null);
        var childContext = new ContextItem(Guid.NewGuid(), depot.Id, child.Id, ContextKind.Preference, "child-key", null, "Child", now.AddHours(-1));
        var failed = new Document(Guid.NewGuid(), depot.Id, root.Id, "failed.md", "Failed document", now.AddHours(-2));
        failed.MarkFailed("test", now.AddHours(-2));
        var pending = new Document(Guid.NewGuid(), depot.Id, child.Id, "pending.md", "", now.AddHours(-3));
        var archived = new Document(Guid.NewGuid(), depot.Id, root.Id, "archived.md", "Archived", now);
        archived.MarkArchived(now);
        var outsider = new Document(Guid.NewGuid(), otherDepot.Id, other.Id, "other.md", "Other", now);
        db.AddRange(active, expired, future, superseded, archivedContext, ended, childContext, failed, pending, archived, outsider);
        await db.SaveChangesAsync();
        var repository = new OverviewRepository(db);
        var spaces = await repository.ListWorkspacesAsync(depot.Id, 2, now, default);
        Assert.True(spaces.HasMore);
        Assert.Equal(child.Id, spaces.Items[0].Id);
        Assert.Equal("root/child", spaces.Items[0].Path);
        var rootSummary = spaces.Items.Single(x => x.Id == root.Id);
        Assert.Equal(1, rootSummary.ContextCount);
        Assert.Equal(1, rootSummary.DocumentCount);
        Assert.Equal(active.UpdatedAt, rootSummary.ActivityAt);
        var recent = await repository.ListKnowledgeAsync(depot.Id, 3, now, default);
        Assert.True(recent.HasMore);
        Assert.Equal(new[] { childContext.Id, active.Id, failed.Id }, recent.Items.Select(x => x.Id));
        Assert.Equal("A decision", recent.Items[1].Title);
        Assert.Equal(DocumentIndexStatus.Failed, recent.Items[2].IndexStatus);
        var all = await repository.ListKnowledgeAsync(depot.Id, 20, now, default);
        Assert.False(all.HasMore);
        Assert.Equal("pending.md", all.Items.Single(x => x.Id == pending.Id).Title);
        Assert.DoesNotContain(all.Items, x => x.Workspace.Id == other.Id);
        await transaction.RollbackAsync();
    }

    [PostgreSqlFact]
    public async Task DuplicateRootCreationCannotOverwriteExistingWorkspace()
    {
        var access = new CurrentDepotAccessContext();
        access.AllowInternalAccess();
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")).Options;
        await using var db = new ContextDepotDbContext(options, access);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.UtcNow;
        var depot = new Depot(Guid.NewGuid(), "Create test", now);
        db.Depots.Add(depot);
        await db.SaveChangesAsync();
        var repository = new OverviewRepository(db);
        var id = Guid.NewGuid();
        Assert.True(await repository.CreateWorkspaceAsync(depot.Id, id, "Original", "research", "Original description", now, default));
        Assert.False(await repository.CreateWorkspaceAsync(depot.Id, Guid.NewGuid(), "Changed", "research", null, now.AddSeconds(1), default));
        var stored = await db.Workspaces.AsNoTracking().SingleAsync(x => x.DepotId == depot.Id);
        Assert.Equal(id, stored.Id);
        Assert.Equal("Original", stored.Name);
        Assert.Equal("Original description", stored.Description);
        var spaces = await repository.ListWorkspacesAsync(depot.Id, 3, now, default);
        Assert.Equal(0, spaces.Items[0].ContextCount);
        Assert.Equal(0, spaces.Items[0].DocumentCount);
        await transaction.RollbackAsync();
    }
}
