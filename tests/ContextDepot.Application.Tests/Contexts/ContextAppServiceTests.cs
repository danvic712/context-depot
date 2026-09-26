using ContextDepot.Application.Contexts;
using ContextDepot.Application.Contexts.Contracts;
using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Shared.Safety.Dtos;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Contexts;
using ContextDepot.Domain.Contexts.Enums;
using ContextDepot.Domain.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Contexts.Enums;
using Moq;

namespace ContextDepot.Application.Tests.Contexts;

public sealed class ContextAppServiceTests
{
    private static readonly Guid DepotId = Guid.Parse("0199c000-0000-7000-8000-000000000001");
    private static readonly Guid WorkspaceId = Guid.Parse("0199c000-0000-7000-8000-000000000002");

    [Fact]
    public async Task Keyed_save_delegates_replacement_to_atomic_repository()
    {
        var previousId = Guid.CreateVersion7();
        var repository = new Mock<IContextRepository>();
        repository
            .Setup(x => x.SaveKeyedAsync(It.IsAny<ContextItem>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((ContextItem candidate, DateTimeOffset _, CancellationToken _) => new ContextPersistenceResult(candidate, ContextPersistenceOutcome.Replaced, previousId));
        var service = CreateService(repository);

        var result = await service.SaveAsync(new SaveContextCommand("projects/context-depot", ContextKind.Decision, "PostgreSQL", "project.database"), CancellationToken.None);

        Assert.Equal(SaveContextOutcome.UpdatedCurrentTruth, result.Outcome);
        Assert.Equal(previousId, result.PreviousContextId);
        repository.Verify(x => x.SaveKeyedAsync(It.IsAny<ContextItem>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Key_kind_conflict_is_exposed_without_writing()
    {
        var existing = new ContextItem(Guid.CreateVersion7(), DepotId, WorkspaceId, ContextKind.Decision, "project.database", null, "SQLite", DateTimeOffset.UtcNow);
        var repository = new Mock<IContextRepository>();
        repository
            .Setup(x => x.SaveKeyedAsync(It.IsAny<ContextItem>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new ContextPersistenceResult(existing, ContextPersistenceOutcome.KindConflict));
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.SaveAsync(new SaveContextCommand("projects/context-depot", ContextKind.Fact, "PostgreSQL", "project.database"), CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.ContextKindConflict, exception.ErrorCode);
        repository.Verify(x => x.SaveKeyedAsync(It.IsAny<ContextItem>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Observation_is_rejected_before_repository_write()
    {
        var repository = new Mock<IContextRepository>();
        var service = CreateService(repository);

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.SaveAsync(new SaveContextCommand("projects/context-depot", ContextKind.Observation, "runtime trace"), CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.InvalidContextKind, exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Invalid_validity_interval_is_rejected_before_repository_write()
    {
        var repository = new Mock<IContextRepository>();
        var service = CreateService(repository);
        var now = DateTimeOffset.Parse("2026-09-19T00:00:00Z");

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.SaveAsync(
            new SaveContextCommand("projects/context-depot", ContextKind.Fact, "content",
                ValidFrom: now, ValidUntil: now), CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.InvalidContextValidity, exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Secret_in_metadata_or_tags_is_rejected_before_repository_write()
    {
        var repository = new Mock<IContextRepository>();
        var service = CreateService(repository, new SourceSafetyService(new HighConfidenceSecretDetector(), new ProvenancePolicy()));

        var exception = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.SaveAsync(
            new SaveContextCommand(
                "projects/context-depot",
                ContextKind.Fact,
                "safe content",
                Tags: ["api_key = abcdefghijklmnop"],
                MetadataJson: "{}"),
            CancellationToken.None));

        Assert.Equal(ApplicationErrorCodes.SecretContentRejected, exception.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    private static ContextAppService CreateService(Mock<IContextRepository> repository, ISourceSafetyService? configuredSafety = null)
    {
        var workspace = new WorkspaceModel(WorkspaceId, DepotId, "projects/context-depot", "ContextDepot", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        var workspaces = new Mock<IWorkspaceAppService>();
        workspaces.Setup(x => x.ResolveAsync("projects/context-depot", It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        var safety = configuredSafety ?? CreateDefaultSafety();
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(DepotId);
        var timeProvider = new Mock<TimeProvider>();
        timeProvider.Setup(x => x.GetUtcNow()).Returns(DateTimeOffset.Parse("2026-09-19T00:00:00Z"));
        var ids = new Mock<IIdGenerator>();
        ids.Setup(x => x.NewId()).Returns(Guid.Parse("0199c000-0000-7000-8000-000000000003"));
        return new ContextAppService(depot.Object, workspaces.Object, repository.Object, safety, ids.Object, timeProvider.Object);
    }

    private static ISourceSafetyService CreateDefaultSafety()
    {
        var safety = new Mock<ISourceSafetyService>();
        safety.Setup(x => x.EvaluateProvenance(It.IsAny<ProvenanceInput>())).Returns(new ProvenanceDecision(VerificationStatus.Unknown, ProvenanceTrust.Asserted, SourceType.Agent, null, null));
        return safety.Object;
    }
}
