using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
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
        repository.Setup(x => x.LoadEnsureSnapshotAsync(DepotId, null, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new WorkspaceWriteSnapshot(null, null, [], new(null, true, true, true, 0, [])));
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

    [Fact]
    public async Task Visible_choices_keep_display_names_paths_and_depot_scope()
    {
        var now = DateTimeOffset.UtcNow;
        var workspace = new Workspace(Guid.CreateVersion7(), DepotId, Guid.CreateVersion7(), "项目笔记", "notes", null, now);
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        using var cancellation = new CancellationTokenSource();
        repository.Setup(x => x.ListVisibleWithPathsAsync(DepotId, cancellation.Token))
            .ReturnsAsync(new[] { new WorkspacePathLookup(workspace, "projects/notes") });

        var result = Assert.Single(await CreateService(repository).ListVisibleAsync(cancellation.Token));

        Assert.Equal(workspace.Id, result.Id);
        Assert.Equal("项目笔记", result.Name);
        Assert.Equal("projects/notes", result.Path);
        repository.Verify(x => x.ListVisibleWithPathsAsync(DepotId, cancellation.Token), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData(" ", "{}", ApplicationErrorCodes.InvalidWorkspaceName)]
    [InlineData("Valid", "[]", ApplicationErrorCodes.InvalidWorkspaceMetadata)]
    [InlineData("Valid", "{", ApplicationErrorCodes.InvalidWorkspaceMetadata)]
    public async Task Invalid_details_are_rejected_before_either_write_mode_accesses_persistence(
        string name, string metadataJson, string errorCode)
    {
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        var service = CreateService(repository);
        var upsertError = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.UpsertAsync(new("projects/context-depot", name, MetadataJson: metadataJson), default));
        var ensureError = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            service.EnsureAsync(new(new(WorkspaceSubjectType.Project, "context-depot"),
                "projects/context-depot", name, MetadataJson: metadataJson), default));

        Assert.Equal(errorCode, upsertError.ErrorCode);
        Assert.Equal(errorCode, ensureError.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    private static WorkspaceAppService CreateService(Mock<IWorkspaceRepository> repository)
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(DepotId);
        var safety = new Mock<ISourceSafetyService>();
        return new WorkspaceAppService(depot.Object, repository.Object, TimeProvider.System, safety.Object,
            new WorkspaceWriter(repository.Object, new Mock<IIdGenerator>().Object, new Mock<IWorkspaceAccessContext>().Object));
    }
}
