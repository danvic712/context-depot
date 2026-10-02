using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Depots;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

public sealed class WorkspaceBrowserRepositoryTests
{
    [PostgreSqlFact]
    public async Task DirectoryPaginatesRootsAndChildrenWithExactCountsAndDepotBoundaries()
    {
        var access = new CurrentDepotAccessContext();
        access.AllowInternalAccess();
        var options = new DbContextOptionsBuilder<ContextDepotDbContext>()
            .UseNpgsql(Environment.GetEnvironmentVariable("CONTEXTDEPOT_TEST_CONNECTION")).Options;
        await using var db = new ContextDepotDbContext(options, access);
        await using var transaction = await db.Database.BeginTransactionAsync();
        var now = DateTimeOffset.Parse("2026-10-02T00:00:00Z");
        var depot = new Depot(Guid.NewGuid(), "Directory test", now);
        var otherDepot = new Depot(Guid.NewGuid(), "Other depot", now);
        var root = new Workspace(Guid.NewGuid(), depot.Id, null, "Root", "root", "A root space", now.AddDays(-2));
        var empty = new Workspace(Guid.NewGuid(), depot.Id, null, "Empty", "empty", null, now.AddDays(-3));
        var child = new Workspace(Guid.NewGuid(), depot.Id, root.Id, "Child", "child", null, now.AddDays(-1));
        var grandchild = new Workspace(Guid.NewGuid(), depot.Id, child.Id, "Deep", "deep", null, now);
        var other = new Workspace(Guid.NewGuid(), otherDepot.Id, null, "Other", "other", null, now);
        var active = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Fact, null, "Active", "Active", now.AddHours(-2));
        var expired = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Fact, null, "Expired", "Expired", now);
        expired.SetValidity(null, null, now);
        var future = new ContextItem(Guid.NewGuid(), depot.Id, root.Id, ContextKind.Fact, null, "Future", "Future", now);
        future.SetValidity(now.AddDays(1), null, null);
        var archived = new Document(Guid.NewGuid(), depot.Id, root.Id, "archived.md", "Archived", now);
        archived.MarkArchived(now);
        var document = new Document(Guid.NewGuid(), depot.Id, root.Id, "active.md", "Active document", now.AddHours(-1));
        document.MarkFailed("test", now.AddHours(-1));
        var childContext = new ContextItem(Guid.NewGuid(), depot.Id, child.Id, ContextKind.Fact, null, "Child fact", "Child fact", now);
        db.AddRange(depot, otherDepot, root, empty, child, grandchild, other, active, expired, future, archived, document, childContext);
        await db.SaveChangesAsync();
        var repository = new WorkspaceBrowserRepository(db);
        var first = await repository.BrowseAsync(depot.Id, null, 1, 1, now, default);
        Assert.Equal(2, first.TotalCount);
        Assert.Equal(root.Id, Assert.Single(first.Items).Id);
        Assert.Equal(1, first.Items[0].ContextCount);
        Assert.Equal(1, first.Items[0].DocumentCount);
        Assert.Equal(1, first.Items[0].SubspaceCount);
        Assert.Equal(document.UpdatedAt, first.Items[0].ActivityAt);
        var second = await repository.BrowseAsync(depot.Id, null, 2, 1, now, default);
        Assert.Equal(empty.Id, Assert.Single(second.Items).Id);
        Assert.Empty((await repository.BrowseAsync(depot.Id, null, 3, 1, now, default)).Items);
        var children = await repository.BrowseAsync(depot.Id, root.Id, 1, 12, now, default);
        Assert.Equal(child.Id, Assert.Single(children.Items).Id);
        Assert.Equal("root/child", children.Items[0].Path);
        var detail = await repository.GetAsync(depot.Id, grandchild.Id, now, default);
        Assert.NotNull(detail);
        Assert.Equal("root/child/deep", detail.Workspace.Path);
        Assert.Equal(new[] { root.Id, child.Id }, detail.Ancestors.Select(x => x.Id));
        Assert.Equal(new[] { "Root", "Child" }, detail.Ancestors.Select(x => x.Name));
        Assert.Null(await repository.GetAsync(depot.Id, other.Id, now, default));
        await transaction.RollbackAsync();
    }
}
