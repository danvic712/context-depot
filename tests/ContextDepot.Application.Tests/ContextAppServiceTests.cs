using ContextDepot.Application.Abstractions;
using ContextDepot.Application.Contexts;
using ContextDepot.Application.Persistence;
using ContextDepot.Application.Safety;
using ContextDepot.Application.Workspaces;
using ContextDepot.Domain.Entities;
using Moq;

namespace ContextDepot.Application.Tests;

public sealed class ContextAppServiceTests
{
    private static readonly Guid OwnerId = Guid.Parse("0199c000-0000-7000-8000-000000000001");
    private static readonly Guid WorkspaceId = Guid.Parse("0199c000-0000-7000-8000-000000000002");

    [Fact]
    public async Task Keyed_save_supersedes_the_previous_current_truth()
    {
        var previous = new ContextItem(Guid.CreateVersion7(), OwnerId, WorkspaceId, ContextKind.Decision, "project.database", "Database", "SQLite", DateTimeOffset.UtcNow);
        var repository = new Mock<IContextRepository>();
        repository.Setup(x => x.GetActiveByKeyAsync(OwnerId, WorkspaceId, "project.database", It.IsAny<CancellationToken>())).ReturnsAsync(previous);
        var service = CreateService(repository);

        var result = await service.SaveAsync(new SaveContextCommand("projects/context-depot", ContextKind.Decision, "PostgreSQL", "project.database"), CancellationToken.None);

        Assert.Equal(SaveContextOutcome.UpdatedCurrentTruth, result.Outcome);
        Assert.Equal(ContextStatus.Superseded, previous.Status);
        Assert.Equal(previous.Id, result.PreviousContextId);
        repository.Verify(x => x.AddAsync(It.IsAny<ContextItem>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Key_kind_conflict_does_not_write()
    {
        var previous = new ContextItem(Guid.CreateVersion7(), OwnerId, WorkspaceId, ContextKind.Decision, "project.database", null, "SQLite", DateTimeOffset.UtcNow);
        var repository = new Mock<IContextRepository>();
        repository.Setup(x => x.GetActiveByKeyAsync(OwnerId, WorkspaceId, "project.database", It.IsAny<CancellationToken>())).ReturnsAsync(previous);
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.SaveAsync(new SaveContextCommand("projects/context-depot", ContextKind.Fact, "PostgreSQL", "project.database"), CancellationToken.None));

        Assert.Equal("ContextKindConflict", exception.ErrorCode);
        repository.Verify(x => x.AddAsync(It.IsAny<ContextItem>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static ContextAppService CreateService(Mock<IContextRepository> repository)
    {
        var workspace = new WorkspaceModel(WorkspaceId, OwnerId, "projects/context-depot", "ContextDepot", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("projects/context-depot", It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        var safety = new Mock<ISourceSafetyService>();
        safety.Setup(x => x.EvaluateProvenance(It.IsAny<ProvenanceInput>())).Returns(new ProvenanceDecision(VerificationStatus.SelfReported, ProvenanceTrust.AgentReported, SourceType.Agent, null, null));
        var owner = new Mock<ICurrentOwnerContext>();
        owner.SetupGet(x => x.OwnerId).Returns(OwnerId);
        var clock = new Mock<IClock>();
        clock.SetupGet(x => x.UtcNow).Returns(DateTimeOffset.Parse("2026-09-19T00:00:00Z"));
        var ids = new Mock<IIdGenerator>();
        ids.Setup(x => x.NewId()).Returns(Guid.Parse("0199c000-0000-7000-8000-000000000003"));
        return new ContextAppService(owner.Object, workspaces.Object, repository.Object, safety.Object, ids.Object, clock.Object);
    }
}
