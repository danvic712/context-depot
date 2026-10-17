using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Dtos;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;
using Moq;

namespace ContextDepot.Application.Tests.Workspaces;

public sealed class WorkspaceBrowserAppServiceTests
{
    private readonly Guid depotId = Guid.NewGuid();
    private readonly Mock<IWorkspaceBrowserRepository> repository = new();
    private readonly DateTimeOffset now = DateTimeOffset.Parse("2026-10-02T00:00:00Z");

    private WorkspaceBrowserAppService CreateService()
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(depotId);
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(now);
        return new(depot.Object, repository.Object, clock.Object);
    }

    [Theory]
    [InlineData((WorkspaceContentStatus)999, WorkspaceContentType.All, WorkspaceSortOrder.Activity, null)]
    [InlineData(WorkspaceContentStatus.All, (WorkspaceContentType)999, WorkspaceSortOrder.Activity, null)]
    [InlineData(WorkspaceContentStatus.All, WorkspaceContentType.All, (WorkspaceSortOrder)999, null)]
    [InlineData(WorkspaceContentStatus.All, WorkspaceContentType.All, WorkspaceSortOrder.Activity, (WorkspaceUpdateWindow)1)]
    [InlineData(WorkspaceContentStatus.Empty, WorkspaceContentType.Documents, WorkspaceSortOrder.Activity, null)]
    public async Task InvalidFiltersNeverReachPersistence(WorkspaceContentStatus status, WorkspaceContentType content,
        WorkspaceSortOrder sort, WorkspaceUpdateWindow? days)
    {
        var query = new WorkspaceDirectoryQuery { Status = status, Content = content, Sort = sort, UpdatedWithinDays = days };
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().BrowseAsync(query, default));
        Assert.Equal(ApplicationErrorCodes.InvalidResourceQuery, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task RejectsOversizedKeywordsAndConflictingDateRanges()
    {
        WorkspaceDirectoryQuery[] queries = [
            new() { Query = new string('x', WorkspaceDirectoryQuery.MaximumQueryLength + 1) },
            new() { UpdatedFrom = now, UpdatedBefore = now },
            new() { UpdatedFrom = now, UpdatedBefore = now.AddDays(-1) },
            new() { UpdatedWithinDays = WorkspaceUpdateWindow.Month, UpdatedFrom = now }
        ];
        foreach (var query in queries)
            await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().BrowseAsync(query, default));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task NormalizesKeywordAndPassesConditionsWithCurrentScopeAndClock()
    {
        var query = new WorkspaceDirectoryQuery { Query = "  project  ", Content = WorkspaceContentType.Both,
            UpdatedWithinDays = WorkspaceUpdateWindow.Month, Sort = WorkspaceSortOrder.Name, Page = 2 };
        var normalized = query with { Query = "project" };
        repository.Setup(x => x.BrowseAsync(depotId, normalized, now, default))
            .ReturnsAsync(new WorkspaceDirectory(now, [], 0, 2, PaginationQuery.DefaultPageSize));
        Assert.Equal(now, (await CreateService().BrowseAsync(query, default)).AsOf);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(0, 12)]
    [InlineData(100001, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 61)]
    public async Task InvalidPaginationNeverReachesPersistence(int page, int pageSize)
    {
        var query = new WorkspaceDirectoryQuery { Page = page, PageSize = pageSize };
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().BrowseAsync(query, default));
        Assert.Equal(ApplicationErrorCodes.InvalidResourceQuery, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BrowseUsesCurrentDepotAndSharesClockWithParentLookup()
    {
        var parentId = Guid.NewGuid();
        var query = new WorkspaceDirectoryQuery { ParentId = parentId, Page = 2 };
        repository.Setup(x => x.GetAsync(depotId, parentId, now, default)).ReturnsAsync(
            new WorkspaceDetail(new(parentId, "Root", null, "root", 0, 0, 0, now), []));
        repository.Setup(x => x.BrowseAsync(depotId, query, now, default)).ReturnsAsync(new WorkspaceDirectory(now, [], 0, 2, 12));
        var result = await CreateService().BrowseAsync(query, default);
        Assert.Equal(now, result.AsOf);
        repository.VerifyAll();
    }

    [Fact]
    public async Task MissingOrOtherDepotParentCannotBeBrowsed()
    {
        var parentId = Guid.NewGuid();
        repository.Setup(x => x.GetAsync(depotId, parentId, now, default)).ReturnsAsync((WorkspaceDetail?)null);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().BrowseAsync(new() { ParentId = parentId }, default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceNotFound, error.ErrorCode);
        repository.Verify(x => x.BrowseAsync(It.IsAny<Guid>(), It.IsAny<WorkspaceDirectoryQuery>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingDetailReturnsTheExistingNotFoundCode()
    {
        var id = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().GetAsync(id, default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceNotFound, error.ErrorCode);
        repository.Verify(x => x.GetAsync(depotId, id, now, default), Times.Once);
    }

    [Fact]
    public async Task KnowledgeListUsesCurrentDepotAndDoesNotExpandToChildSpaces()
    {
        var id = Guid.CreateVersion7();
        var query = new PaginationQuery { Page = 2 };
        repository.Setup(x => x.ListKnowledgeAsync(depotId, id, query, now, default))
            .ReturnsAsync(new WorkspaceKnowledge(now, [], 0, 2, 12));
        var result = await CreateService().ListKnowledgeAsync(id, query, default);
        Assert.Equal(2, result.Page);
        repository.VerifyAll();
    }
}
