using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Domain.Workspaces;
using Moq;

namespace ContextDepot.Application.Tests.Workspaces;

public sealed class WorkspaceAppServiceTests
{
    private static readonly Guid DepotId = Guid.CreateVersion7();

    [Fact]
    public async Task Missing_parent_is_reported_without_fallback_writes()
    {
        var repository = new Mock<IWorkspaceRepository>();
        repository
            .Setup(x => x.UpsertPathAsync(DepotId, "projects/context-depot", "ContextDepot", null, "{}", false, It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceUpsertPersistenceResult(null, WorkspaceUpsertPersistenceOutcome.ParentNotFound));
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.UpsertAsync(new UpsertWorkspaceCommand("projects/context-depot", "ContextDepot"), CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.WorkspaceParentNotFound, exception.ErrorCode);
    }

    [Fact]
    public async Task Get_returns_the_repository_computed_full_path()
    {
        var workspace = new Workspace(Guid.CreateVersion7(), DepotId, Guid.CreateVersion7(), "Context Depot", "context-depot", null, DateTimeOffset.UtcNow);
        var repository = new Mock<IWorkspaceRepository>();
        repository.Setup(x => x.GetByIdWithPathAsync(DepotId, workspace.Id, It.IsAny<CancellationToken>())).ReturnsAsync(new WorkspacePathLookup(workspace, "projects/context-depot"));
        var service = CreateService(repository);

        var result = await service.GetAsync(workspace.Id, CancellationToken.None);

        Assert.Equal("projects/context-depot", result!.Path);
    }

    private static WorkspaceAppService CreateService(Mock<IWorkspaceRepository> repository)
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(DepotId);
        var safety = new Mock<ISourceSafetyService>();
        return new WorkspaceAppService(depot.Object, repository.Object, TimeProvider.System, safety.Object);
    }
}
