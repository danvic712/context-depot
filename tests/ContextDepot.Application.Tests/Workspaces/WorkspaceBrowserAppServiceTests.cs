using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
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
    [InlineData(0, 12)]
    [InlineData(100001, 12)]
    [InlineData(1, 0)]
    [InlineData(1, 61)]
    public async Task InvalidPaginationNeverReachesPersistence(int page, int pageSize)
    {
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().BrowseAsync(null, page, pageSize, default));
        Assert.Equal(ApplicationErrorCodes.InvalidResourceQuery, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task BrowseUsesCurrentDepotAndSharesClockWithParentLookup()
    {
        var parentId = Guid.NewGuid();
        repository.Setup(x => x.GetAsync(depotId, parentId, now, default)).ReturnsAsync(
            new WorkspaceDetail(new(parentId, "Root", null, "root", 0, 0, 0, now), []));
        repository.Setup(x => x.BrowseAsync(depotId, parentId, 2, 12, now, default)).ReturnsAsync(new WorkspaceDirectory(now, [], 0, 2, 12));
        var result = await CreateService().BrowseAsync(parentId, 2, 12, default);
        Assert.Equal(now, result.AsOf);
        repository.VerifyAll();
    }

    [Fact]
    public async Task MissingOrOtherDepotParentCannotBeBrowsed()
    {
        var parentId = Guid.NewGuid();
        repository.Setup(x => x.GetAsync(depotId, parentId, now, default)).ReturnsAsync((WorkspaceDetail?)null);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().BrowseAsync(parentId, 1, 12, default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceNotFound, error.ErrorCode);
        repository.Verify(x => x.BrowseAsync(It.IsAny<Guid>(), It.IsAny<Guid?>(), It.IsAny<int>(), It.IsAny<int>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task MissingDetailReturnsTheExistingNotFoundCode()
    {
        var id = Guid.NewGuid();
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().GetAsync(id, default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceNotFound, error.ErrorCode);
        repository.Verify(x => x.GetAsync(depotId, id, now, default), Times.Once);
    }
}
