using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Access;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Documents.Enums;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using ContextDepot.Application.Overview;
using ContextDepot.Application.Shared.Exceptions;

namespace ContextDepot.Infrastructure.Tests;

public sealed class PostgreSqlFactAttribute : FactAttribute
{
    public PostgreSqlFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")))
            Skip = "Set CONTEXTDEPOT_TEST_CONNECTION for PostgreSQL integration tests. Tests use isolated schemas.";
    }
}

public sealed class OverviewRepositoryTests
{
    [PostgreSqlFact]
    public async Task CollectionsRespectLifecycleDirectCountsPathsAndStableOrdering()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(unrestricted: true);
        var now = DateTimeOffset.Parse("2026-10-01T00:00:00Z");


        var root = new Workspace(Guid.NewGuid(), null, "Root", "root", null, now.AddDays(-2));
        var child = new Workspace(Guid.NewGuid(), root.Id, "Child", "child", null, now.AddDays(-2));
        var empty = new Workspace(Guid.NewGuid(), null, "Empty", "empty", null, now.AddDays(-3));
        var other = new Workspace(Guid.NewGuid(), null, "Other", "other", null, now);
        db.AddRange(root, child, empty, other);
        var active = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Decision, null, "", "**A decision**\n\nDetails", now.AddHours(-2));
        var expired = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Fact, null, "Expired", "Expired", now);
        expired.SetValidity(null, null, now);
        var future = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Fact, null, "Future", "Future", now);
        future.SetValidity(now.AddDays(1), null, null);
        var superseded = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Fact, null, "Old", "Old", now);
        superseded.MarkSuperseded(now);
        var archivedContext = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Fact, null, "Archived", "Archived", now);
        archivedContext.MarkArchived(now);
        var ended = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Fact, null, "Ended", "Ended", now);
        ended.SetValidity(now.AddDays(-1), now, null);
        var childContext = new ContextItem(Guid.NewGuid(), child.Id, ContextKind.Preference, "child-key", null, "Child", now.AddHours(-1));
        var failed = new Document(Guid.NewGuid(), root.Id, "failed.md", "Failed document", now.AddHours(-2));
        failed.MarkFailed("test", now.AddHours(-2));
        var pending = new Document(Guid.NewGuid(), child.Id, "pending.md", "", now.AddHours(-3));
        var archived = new Document(Guid.NewGuid(), root.Id, "archived.md", "Archived", now);
        archived.MarkArchived(now);
        var outsider = new Document(Guid.NewGuid(), other.Id, "other.md", "Other", now);
        db.AddRange(active, expired, future, superseded, archivedContext, ended, childContext, failed, pending, archived, outsider);
        await db.SaveChangesAsync();
        var repository = new OverviewRepository(db);
        var spaces = await repository.ListWorkspacesAsync(3, now, default);
        Assert.True(spaces.HasMore);
        Assert.Equal(other.Id, spaces.Items[0].Id);
        Assert.Equal(child.Id, spaces.Items[1].Id);
        Assert.Equal("root/child", spaces.Items[1].Path);
        var rootSummary = spaces.Items.Single(x => x.Id == root.Id);
        Assert.Equal(1, rootSummary.ContextCount);
        Assert.Equal(1, rootSummary.DocumentCount);
        Assert.Equal(active.UpdatedAt, rootSummary.ActivityAt);
        var recent = await repository.ListKnowledgeAsync(4, now, default);
        Assert.True(recent.HasMore);
        Assert.Equal(new[] { outsider.Id, childContext.Id, active.Id, failed.Id }, recent.Items.Select(x => x.Id));
        Assert.Equal("A decision", recent.Items[2].Title);
        Assert.Equal(DocumentIndexStatus.Failed, recent.Items[3].IndexStatus);
        var all = await repository.ListKnowledgeAsync(20, now, default);
        Assert.False(all.HasMore);
        Assert.Equal("pending.md", all.Items.Single(x => x.Id == pending.Id).Title);
        Assert.Contains(all.Items, x => x.Workspace.Id == other.Id);
        var scoped = await repository.ListRecentKnowledgeAsync(1,
            new RecentKnowledgeFilter("root/child", "documents", null), now, default);
        Assert.Equal(pending.Id, Assert.Single(scoped.Items).Id);
        var decisions = await repository.ListRecentKnowledgeAsync(1,
            new RecentKnowledgeFilter(null, null, ContextKind.Decision), now, default);
        Assert.Equal(active.Id, Assert.Single(decisions.Items).Id);
        Assert.False(decisions.HasMore);
        var missing = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            repository.ListRecentKnowledgeAsync(1, new("missing", null, null), now, default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceNotFound, missing.ErrorCode);
    }

    [PostgreSqlFact]
    public async Task DuplicateRootCreationCannotOverwriteExistingWorkspace()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(unrestricted: true);
        var now = DateTimeOffset.UtcNow;

        await db.SaveChangesAsync();
        var repository = new OverviewRepository(db);
        var id = Guid.NewGuid();
        Assert.True(await repository.CreateWorkspaceAsync(id, "Original", "research", "Original description", now, default));
        Assert.False(await repository.CreateWorkspaceAsync(Guid.NewGuid(), "Changed", "research", null, now.AddSeconds(1), default));
        var stored = await db.Workspaces.AsNoTracking().SingleAsync(x => x.Id == id);
        Assert.Equal(id, stored.Id);
        Assert.Equal("Original", stored.Name);
        Assert.Equal("Original description", stored.Description);
        var spaces = await repository.ListWorkspacesAsync(3, now, default);
        Assert.Equal(0, spaces.Items[0].ContextCount);
        Assert.Equal(0, spaces.Items[0].DocumentCount);
    }
}
