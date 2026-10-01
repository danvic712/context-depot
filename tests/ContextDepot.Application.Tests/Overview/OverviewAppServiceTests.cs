using ContextDepot.Application.Overview;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using Moq;

namespace ContextDepot.Application.Tests.Overview;

public sealed class OverviewAppServiceTests
{
    private readonly Guid depotId = Guid.NewGuid();
    private readonly Mock<IOverviewRepository> repository = new();
    private readonly Mock<ISourceSafetyService> safety = new();
    private readonly DateTimeOffset now = DateTimeOffset.Parse("2026-10-01T00:00:00Z");

    private OverviewAppService CreateService()
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(depotId);
        var ids = new Mock<IIdGenerator>();
        ids.Setup(x => x.NewId()).Returns(Guid.NewGuid);
        var clock = new Mock<TimeProvider>();
        clock.Setup(x => x.GetUtcNow()).Returns(now);
        return new(depot.Object, repository.Object, ids.Object, clock.Object, safety.Object);
    }

    [Theory]
    [InlineData("", "research")]
    [InlineData("Research", "parent/child")]
    [InlineData("Research", "Research")]
    [InlineData("Research", "research--notes")]
    [InlineData("Research", "../research")]
    public async Task InvalidRootDoesNotReachPersistence(string name, string path)
    {
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().CreateWorkspaceAsync(new(name, path, null), default));
        Assert.Equal(name.Length == 0 ? ApplicationErrorCodes.InvalidWorkspaceName : ApplicationErrorCodes.InvalidWorkspacePath, error.ErrorCode);
        Assert.Equal(error.ErrorCode, error.Message);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CreateTrimsFieldsChecksSafetyAndUsesCurrentDepot()
    {
        repository.Setup(x => x.CreateWorkspaceAsync(depotId, It.IsAny<Guid>(), "Research", "research-notes", "Notes", now, default)).ReturnsAsync(true);
        var result = await CreateService().CreateWorkspaceAsync(new(" Research ", " research-notes ", " Notes "), default);
        Assert.NotNull(result);
        Assert.Equal("research-notes", result.Path);
        Assert.Equal(now, result.ActivityAt);
        Assert.Equal(0, result.ContextCount);
        Assert.Equal(0, result.DocumentCount);
        safety.Verify(x => x.EnsureSafe("Research"), Times.Once);
        safety.Verify(x => x.EnsureSafe("Notes"), Times.Once);
        repository.VerifyAll();
    }

    [Fact]
    public async Task ExistingSlugThrowsConflictWithoutAnUpsert()
    {
        repository.Setup(x => x.CreateWorkspaceAsync(depotId, It.IsAny<Guid>(), "Research", "research", null, now, default)).ReturnsAsync(false);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().CreateWorkspaceAsync(new("Research", "research", null), default));
        Assert.Equal(ApplicationErrorCodes.WorkspacePathConflict, error.ErrorCode);
        Assert.Equal([ApplicationErrorCodes.WorkspacePathConflict], error.FieldErrorCodes!["path"]);
        repository.VerifyAll();
    }

    [Theory]
    [InlineData(0, "-activityAt")]
    [InlineData(9, "-activityAt")]
    [InlineData(3, "name")]
    public async Task InvalidWorkspaceQueryIsRejected(int limit, string sort)
    {
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => CreateService().ListWorkspacesAsync(limit, sort, default));
        Assert.Equal(ApplicationErrorCodes.InvalidResourceQuery, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task InvalidFieldsAreCollectedAsCodesWithoutSelectingALanguage()
    {
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            CreateService().CreateWorkspaceAsync(new("", "../invalid", null), default));
        Assert.Equal([ApplicationErrorCodes.InvalidWorkspaceName], error.FieldErrorCodes!["name"]);
        Assert.Equal([ApplicationErrorCodes.InvalidWorkspacePath], error.FieldErrorCodes["path"]);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(0, "-updatedAt")]
    [InlineData(21, "-updatedAt")]
    [InlineData(3, "name")]
    public async Task InvalidKnowledgeQueryIsRejected(int limit, string sort)
    {
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            CreateService().ListKnowledgeAsync(limit, sort, default));
        Assert.Equal(ApplicationErrorCodes.InvalidResourceQuery, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task CollectionsShareTheClockAndAreScopedToTheCurrentDepot()
    {
        repository.Setup(x => x.ListWorkspacesAsync(depotId, 3, now, default)).ReturnsAsync(new ResourceCollection<WorkspaceSummary>(now, [], false));
        repository.Setup(x => x.ListKnowledgeAsync(depotId, 3, now, default)).ReturnsAsync(new ResourceCollection<KnowledgeSummary>(now, [], false));
        var service = CreateService();
        await service.ListWorkspacesAsync(3, "-activityAt", default);
        await service.ListKnowledgeAsync(3, "-updatedAt", default);
        repository.VerifyAll();
    }
}
