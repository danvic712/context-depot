using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Documents;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Infrastructure.Access;
using ContextDepot.Infrastructure.Repositories;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Application.Shared.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Tests;

public sealed class WorkspaceBrowserRepositoryTests
{
    [PostgreSqlFact]
    public async Task DirectoryListsPeerSpacesAndSupportsExplicitParentScopeWithExactCounts()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        await using var db = fixture.CreateDb(unrestricted: true);
        var now = DateTimeOffset.Parse("2026-10-02T00:00:00Z");


        var root = new Workspace(Guid.NewGuid(), null, "Root", "root", "A root space", now.AddDays(-2));
        var empty = new Workspace(Guid.NewGuid(), null, "Empty", "empty", null, now.AddDays(-3));
        var child = new Workspace(Guid.NewGuid(), root.Id, "Child", "child", null, now.AddDays(-1));
        var grandchild = new Workspace(Guid.NewGuid(), child.Id, "Deep", "deep", null, now.AddMinutes(-30));
        var other = new Workspace(Guid.NewGuid(), null, "Other", "other", null, now.AddMinutes(-5));
        var active = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Fact, null, "Active", "Active", now.AddHours(-2));
        var expired = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Fact, null, "Expired", "Expired", now);
        expired.SetValidity(null, null, now);
        var future = new ContextItem(Guid.NewGuid(), root.Id, ContextKind.Fact, null, "Future", "Future", now);
        future.SetValidity(now.AddDays(1), null, null);
        var archived = new Document(Guid.NewGuid(), root.Id, "archived.md", "Archived", now);
        archived.MarkArchived(now);
        var document = new Document(Guid.NewGuid(), root.Id, "active.md", "Active document", now.AddHours(-1));
        document.MarkFailed("test", now.AddHours(-1));
        var childContext = new ContextItem(Guid.NewGuid(), child.Id, ContextKind.Fact, null, "Child fact", "Child fact", now);
        db.AddRange(root, empty, child, grandchild, other, active, expired, future, archived, document, childContext);
        await db.SaveChangesAsync();
        var repository = new WorkspaceBrowserRepository(db);
        var first = await repository.BrowseAsync(new WorkspaceDirectoryQuery { Page = 1, PageSize = 1 }, now, default);
        Assert.Equal(5, first.TotalCount);
        Assert.Equal(child.Id, Assert.Single(first.Items).Id);
        var all = await repository.BrowseAsync(new WorkspaceDirectoryQuery { Page = 1, PageSize = 12 }, now, default);
        var rootItem = Assert.Single(all.Items, item => item.Id == root.Id);
        Assert.Equal(1, rootItem.ContextCount);
        Assert.Equal(1, rootItem.DocumentCount);
        Assert.Equal(1, rootItem.SubspaceCount);
        Assert.Equal(document.UpdatedAt, rootItem.ActivityAt);
        var second = await repository.BrowseAsync(new WorkspaceDirectoryQuery { Page = 2, PageSize = 1 }, now, default);
        Assert.Equal(other.Id, Assert.Single(second.Items).Id);
        Assert.Equal(empty.Id, Assert.Single((await repository.BrowseAsync(new WorkspaceDirectoryQuery { Page = 5, PageSize = 1 }, now, default)).Items).Id);
        Assert.Empty((await repository.BrowseAsync(new WorkspaceDirectoryQuery { Page = 6, PageSize = 1 }, now, default)).Items);
        var children = await repository.BrowseAsync(new WorkspaceDirectoryQuery { Page = 1, PageSize = 12, ParentId = root.Id }, now, default);
        Assert.Equal(child.Id, Assert.Single(children.Items).Id);
        Assert.Equal("root/child", children.Items[0].Path);
        var detail = await repository.GetAsync(grandchild.Id, now, default);
        Assert.NotNull(detail);
        Assert.Equal("root/child/deep", detail.Workspace.Path);
        Assert.Equal(new[] { root.Id, child.Id }, detail.Ancestors.Select(x => x.Id));
        Assert.Equal(new[] { "Root", "Child" }, detail.Ancestors.Select(x => x.Name));
        Assert.Equal(other.Id, (await repository.GetAsync(other.Id, now, default))!.Workspace.Id);
        var knowledge = await repository.ListKnowledgeAsync(root.Id, new PaginationQuery { Page = 1, PageSize = 1 }, now, default);
        Assert.NotNull(knowledge);
        Assert.Equal(2, knowledge.TotalCount);
        Assert.Equal(document.Id, Assert.Single(knowledge.Items).Id);
        Assert.Equal("document", knowledge.Items[0].Type);
        var contextsPage = await repository.ListKnowledgeAsync(root.Id, new PaginationQuery { Page = 2, PageSize = 1 }, now, default);
        Assert.Equal(active.Id, Assert.Single(contextsPage!.Items).Id);
        Assert.Equal("root", contextsPage.Items[0].Workspace.Path);
        Assert.Empty((await repository.ListKnowledgeAsync(root.Id, new PaginationQuery { Page = 3, PageSize = 1 }, now, default))!.Items);
        Assert.Empty((await repository.ListKnowledgeAsync(other.Id, new PaginationQuery { Page = 1, PageSize = 12 }, now, default))!.Items);
    }

    [PostgreSqlFact]
    public async Task FiltersBeforeCountingAndPagingWithLiteralKeywordsDatesAndAccessBoundaries()
    {
        await using var fixture = await SetupDatabaseFixture.CreateAsync();
        var now = DateTimeOffset.Parse("2026-10-12T12:00:00Z");


        var spaces = Enumerable.Range(0, 15).Select(index => new Workspace(Guid.CreateVersion7(), null,
            $"Project {index:00}", $"needle-{index:00}", index == 0 ? "NEEDLE 100% literal_ C:\\notes" : "NEEDLE notes", now.AddDays(-index))).ToArray();
        var empty = new Workspace(Guid.CreateVersion7(), null, "Empty", "empty", null, now);
        var expiredOnly = new Workspace(Guid.CreateVersion7(), null, "Expired only", "expired", null, now);
        var hidden = new Workspace(Guid.CreateVersion7(), null, "NEEDLE hidden", "needle-hidden", null, now.AddHours(1));
        var foreign = new Workspace(Guid.CreateVersion7(), null, "NEEDLE foreign", "needle-foreign", null, now.AddHours(1));
        var expired = new ContextItem(Guid.CreateVersion7(), expiredOnly.Id, ContextKind.Fact, null, "Expired", "Expired", now);
        expired.SetValidity(null, null, now);
        await using (var seed = fixture.CreateDb(true))
        {
            seed.AddRange(empty, expiredOnly, hidden, foreign, expired);
            seed.AddRange(spaces);
            for (var index = 0; index < spaces.Length; index++)
            {
                seed.Add(new Document(Guid.CreateVersion7(), spaces[index].Id, "notes.md", "Notes", now.AddDays(-index)));
                if (index % 2 == 0) seed.Add(new ContextItem(Guid.CreateVersion7(), spaces[index].Id,
                    ContextKind.Decision, null, "Decision", "Decision", now.AddDays(-index)));
            }
            seed.Add(new Document(Guid.CreateVersion7(), hidden.Id, "hidden.md", "Hidden", now));
            seed.Add(new Document(Guid.CreateVersion7(), foreign.Id, "foreign.md", "Foreign", now));
            await seed.SaveChangesAsync();
        }
        var visible = spaces.Select(space => space.Id).Append(empty.Id).Append(expiredOnly.Id).ToArray();
        var access = new WorkspaceAccessContext();
        access.Initialize(new AccessKeyIdentity(Guid.CreateVersion7(), visible, visible));
        await using var scoped = fixture.CreateDb(access);
        var repository = new WorkspaceBrowserRepository(scoped);
        var filter = new WorkspaceDirectoryQuery { Query = "nEeDlE", Content = WorkspaceContentType.Documents, UpdatedWithinDays = WorkspaceUpdateWindow.Month };
        var first = await repository.BrowseAsync(filter with { Page = 1, PageSize = 2 }, now, default);
        var second = await repository.BrowseAsync(filter with { Page = 2, PageSize = 2 }, now, default);
        Assert.Equal(15, first.TotalCount);
        Assert.Equal(spaces.Take(2).Select(space => space.Id), first.Items.Select(space => space.Id));
        Assert.Equal(spaces.Skip(2).Take(2).Select(space => space.Id), second.Items.Select(space => space.Id));
        Assert.Equal(8, (await repository.BrowseAsync(filter with { Content = WorkspaceContentType.Both }, now, default)).TotalCount);
        Assert.Equal(8, (await repository.BrowseAsync(filter with { UpdatedWithinDays = WorkspaceUpdateWindow.Week }, now, default)).TotalCount);
        var dateRange = filter with { UpdatedWithinDays = null, UpdatedFrom = now.AddDays(-2), UpdatedBefore = now };
        Assert.Equal(spaces.Skip(1).Take(2).Select(space => space.Id),
            (await repository.BrowseAsync(dateRange with { Page = 1, PageSize = 12 }, now, default)).Items.Select(space => space.Id));
        foreach (var keyword in new[] { "%", "_", "\\" })
            Assert.Equal(spaces[0].Id, Assert.Single((await repository.BrowseAsync(new WorkspaceDirectoryQuery { Query = keyword }, now, default)).Items).Id);
        Assert.Equal(spaces[12].Id, Assert.Single((await repository.BrowseAsync(new WorkspaceDirectoryQuery { Query = "needle-12" }, now, default)).Items).Id);
        Assert.Equal(spaces[13].Id, Assert.Single((await repository.BrowseAsync(new WorkspaceDirectoryQuery { Query = "PROJECT 13" }, now, default)).Items).Id);
        var emptyResult = await repository.BrowseAsync(new WorkspaceDirectoryQuery { Status = WorkspaceContentStatus.Empty }, now, default);
        Assert.Equal(2, emptyResult.TotalCount);
        Assert.All(emptyResult.Items, space => Assert.Equal(0, space.ContextCount + space.DocumentCount));
        Assert.Equal(15, (await repository.BrowseAsync(new WorkspaceDirectoryQuery { Status = WorkspaceContentStatus.Populated }, now, default)).TotalCount);
        Assert.Equal(spaces[0].Id, Assert.Single((await repository.BrowseAsync(new WorkspaceDirectoryQuery { Sort = WorkspaceSortOrder.Name, Status = WorkspaceContentStatus.Populated, PageSize = 1 }, now, default)).Items).Id);
        var mostContent = Assert.Single((await repository.BrowseAsync(new WorkspaceDirectoryQuery { Sort = WorkspaceSortOrder.Knowledge, PageSize = 1 }, now, default)).Items);
        Assert.Equal(2, mostContent.ContextCount + mostContent.DocumentCount);
    }
}
