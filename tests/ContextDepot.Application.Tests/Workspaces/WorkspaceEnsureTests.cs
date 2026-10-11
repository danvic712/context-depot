using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Workspaces;
using Moq;

namespace ContextDepot.Application.Tests.Workspaces;

public sealed class WorkspaceEnsureTests
{
    private readonly Guid depotId = Guid.CreateVersion7();
    private readonly DateTimeOffset now = DateTimeOffset.UtcNow;
    private readonly WorkspaceSubject subject = new(WorkspaceSubjectType.Project, "context-depot");
    private readonly Mock<IIdGenerator> ids = new();

    public WorkspaceEnsureTests() => ids.Setup(item => item.NewId()).Returns(() => Guid.CreateVersion7());

    [Fact]
    public void Bound_target_wins_over_proposed_path_without_overwriting_details()
    {
        var existing = Workspace("actual", "Original");
        var snapshot = Snapshot([existing], [existing.Workspace.Id], true, bound: existing.Workspace.Id);
        var plan = WorkspaceEnsurePolicy.Ensure(snapshot, Command("different"), depotId, ids.Object, now);
        Assert.Same(existing, plan.Target);
        Assert.False(plan.UpdateTarget);
        Assert.Empty(plan.Created);
        Assert.Null(plan.NewBinding);
        Assert.Equal("Original", plan.Target.Workspace.Name);
        Assert.Equal(WorkspaceUpsertPersistenceOutcome.ReusedExisting, plan.Outcome);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void Hidden_binding_and_path_collision_are_unavailable_even_with_creation_capability(bool bound)
    {
        var hidden = Workspace("existing");
        var snapshot = Snapshot([hidden], [], true, bound: bound ? hidden.Workspace.Id : null);
        var error = Assert.Throws<ContextDepotApplicationException>(() =>
            WorkspaceEnsurePolicy.Ensure(snapshot, Command(bound ? "other" : "existing"), depotId, ids.Object, now));
        Assert.Equal(ApplicationErrorCodes.WorkspaceUnavailable, error.ErrorCode);
        ids.Verify(item => item.NewId(), Times.Never);
    }

    [Fact]
    public void Readable_path_is_bound_without_updating_existing_management_fields()
    {
        var existing = Workspace("existing", "Original");
        var plan = WorkspaceEnsurePolicy.Ensure(Snapshot([existing], [existing.Workspace.Id]), Command("existing"), depotId, ids.Object, now);
        Assert.Same(existing, plan.Target);
        Assert.False(plan.UpdateTarget);
        Assert.Empty(plan.Created);
        Assert.Equal(existing.Workspace.Id, plan.NewBinding!.WorkspaceId);
        Assert.Equal(subject.Key, plan.NewBinding.SubjectKey);
        Assert.Equal(7, plan.NewBinding.Id.Version);
    }

    [Theory]
    [InlineData(false, false)]
    [InlineData(true, true)]
    public void Root_creation_requires_explicit_capability(bool capability, bool allowed)
    {
        var snapshot = Snapshot([], [], capability);
        if (!allowed)
        {
            Assert.Equal(ApplicationErrorCodes.WorkspaceUnavailable,
                Assert.Throws<ContextDepotApplicationException>(() => WorkspaceEnsurePolicy.Ensure(snapshot, Command("new"), depotId, ids.Object, now)).ErrorCode);
            return;
        }
        var plan = WorkspaceEnsurePolicy.Ensure(snapshot, Command("new"), depotId, ids.Object, now);
        Assert.Single(plan.Created);
        Assert.Equal(plan.Target.Workspace.Id, plan.NewBinding!.WorkspaceId);
        Assert.Null(plan.Target.Workspace.ParentWorkspaceId);
        Assert.Equal(7, plan.Target.Workspace.Id.Version);
        Assert.Equal(WorkspaceUpsertPersistenceOutcome.Created, plan.Outcome);
    }

    [Fact]
    public void Navigation_only_parent_cannot_authorize_creation_and_Ensure_does_not_create_missing_parents()
    {
        var parent = Workspace("parent");
        var child = Workspace("parent/child");
        var snapshot = Snapshot([parent, child], [child.Workspace.Id], true);
        foreach (var path in new[] { "parent/new", "missing/new" })
            Assert.Equal(ApplicationErrorCodes.WorkspaceUnavailable,
                Assert.Throws<ContextDepotApplicationException>(() => WorkspaceEnsurePolicy.Ensure(snapshot, Command(path), depotId, ids.Object, now)).ErrorCode);
    }

    [Fact]
    public void Upsert_retains_multiple_descendant_creation_under_an_exact_grant()
    {
        var parent = Workspace("parent");
        var plan = WorkspaceEnsurePolicy.Upsert(Snapshot([parent], [parent.Workspace.Id]),
            new("parent/new/deep", "Deep", CreateMissingParents: true), depotId, ids.Object, now);
        Assert.Equal(2, plan.Created.Count);
        Assert.Equal(parent.Workspace.Id, plan.Created[0].ParentWorkspaceId);
        Assert.Equal(plan.Created[0].Id, plan.Created[1].ParentWorkspaceId);
    }

    [Fact]
    public void Upsert_updates_details_while_preserving_the_snapshot_for_concurrency_checks()
    {
        var existing = Workspace("existing", "Original");
        existing.Workspace.Update("Original", "Old description", "{\"label\":\"old\"}", now.AddDays(-1));
        var snapshot = Snapshot([existing], [existing.Workspace.Id]);
        var plan = WorkspaceEnsurePolicy.Upsert(snapshot,
            new("existing", "Renamed", "New description", "{\"label\":\"new\"}"), depotId, ids.Object, now);

        Assert.Equal(WorkspaceUpsertPersistenceOutcome.Updated, plan.Outcome);
        Assert.Empty(plan.Created);
        Assert.Equal(existing.Workspace.Id, plan.Target.Workspace.Id);
        Assert.Equal("existing", plan.Target.Path);
        Assert.Equal("Renamed", plan.Target.Workspace.Name);
        Assert.Equal("New description", plan.Target.Workspace.Description);
        Assert.Equal("{\"label\":\"new\"}", plan.Target.Workspace.MetadataJson);
        Assert.Equal("Original", snapshot.Workspaces[0].Workspace.Name);
        Assert.Equal("Old description", snapshot.Workspaces[0].Workspace.Description);
        Assert.Equal("{\"label\":\"old\"}", snapshot.Workspaces[0].Workspace.MetadataJson);
    }

    [Fact]
    public async Task Competition_reloads_and_reuses_same_Key_creation_without_granting_another_Key()
    {
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        var access = new Mock<IWorkspaceAccessContext>(MockBehavior.Strict);
        var first = Snapshot([], [], true);
        var existing = Workspace("new");
        var second = Snapshot([existing], [existing.Workspace.Id], true, existing.Workspace.Id);
        repository.SetupSequence(item => item.LoadEnsureSnapshotAsync(depotId, subject, default)).ReturnsAsync(first).ReturnsAsync(second);
        repository.SetupSequence(item => item.TryApplyEnsureAsync(depotId, It.IsAny<WorkspaceWritePlan>(), now, default))
            .ReturnsAsync(new WorkspaceWritePersistenceResult(false, 0)).ReturnsAsync(new WorkspaceWritePersistenceResult(true, 1));
        access.Setup(item => item.UpdateWorkspaceAccess(It.Is<IReadOnlyCollection<Guid>>(values => values.SequenceEqual(new[] { existing.Workspace.Id })),
            It.Is<IReadOnlyCollection<Guid>>(values => values.SequenceEqual(new[] { existing.Workspace.Id })), true, 1));
        var writer = new WorkspaceWriter(repository.Object, ids.Object, access.Object);
        var plan = await writer.EnsureAsync(depotId, Command("new"), now, default);
        Assert.Empty(plan.Created);
        Assert.Null(plan.NewBinding);
        access.VerifyAll();

        repository = new(MockBehavior.Strict);
        repository.SetupSequence(item => item.LoadEnsureSnapshotAsync(depotId, subject, default)).ReturnsAsync(first)
            .ReturnsAsync(Snapshot([existing], [], true, existing.Workspace.Id));
        repository.Setup(item => item.TryApplyEnsureAsync(depotId, It.IsAny<WorkspaceWritePlan>(), now, default))
            .ReturnsAsync(new WorkspaceWritePersistenceResult(false, 0));
        var deniedAccess = new Mock<IWorkspaceAccessContext>(MockBehavior.Strict);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            new WorkspaceWriter(repository.Object, ids.Object, deniedAccess.Object).EnsureAsync(depotId, Command("new"), now, default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceUnavailable, error.ErrorCode);
        deniedAccess.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Newly_authorized_child_updates_navigation_without_granting_ancestors()
    {
        var parent = Workspace("parent");
        var child = new WorkspacePathLookup(new Workspace(Guid.CreateVersion7(), depotId, parent.Workspace.Id,
            "Child", "child", null, now), "parent/child");
        var repository = new Mock<IWorkspaceRepository>();
        repository.Setup(item => item.LoadEnsureSnapshotAsync(depotId, subject, default))
            .ReturnsAsync(Snapshot([parent, child], [child.Workspace.Id]));
        repository.Setup(item => item.TryApplyEnsureAsync(depotId, It.IsAny<WorkspaceWritePlan>(), now, default))
            .ReturnsAsync(new WorkspaceWritePersistenceResult(true, 1));
        var access = new Mock<IWorkspaceAccessContext>(MockBehavior.Strict);
        access.Setup(item => item.UpdateWorkspaceAccess(
            It.Is<IReadOnlyCollection<Guid>>(values => values.Count == 1 && values.Contains(child.Workspace.Id)),
            It.Is<IReadOnlyCollection<Guid>>(values => values.Count == 2 && values.Contains(child.Workspace.Id) && values.Contains(parent.Workspace.Id)), false, 1));
        await new WorkspaceWriter(repository.Object, ids.Object, access.Object).EnsureAsync(depotId, Command("parent/child"), now, default);
        access.VerifyAll();
    }

    [Fact]
    public async Task Repeated_competition_is_bounded_and_never_updates_request_permissions()
    {
        var repository = new Mock<IWorkspaceRepository>();
        repository.Setup(item => item.LoadEnsureSnapshotAsync(depotId, subject, default)).ReturnsAsync(Snapshot([], [], true));
        repository.Setup(item => item.TryApplyEnsureAsync(depotId, It.IsAny<WorkspaceWritePlan>(), now, default))
            .ReturnsAsync(new WorkspaceWritePersistenceResult(false, 0));
        var access = new Mock<IWorkspaceAccessContext>(MockBehavior.Strict);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() =>
            new WorkspaceWriter(repository.Object, ids.Object, access.Object).EnsureAsync(depotId, Command("new"), now, default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceConcurrencyConflict, error.ErrorCode);
        repository.Verify(item => item.TryApplyEnsureAsync(depotId, It.IsAny<WorkspaceWritePlan>(), now, default), Times.Exactly(2));
        access.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Resolution_hides_inaccessible_binding_and_normalizes_the_subject()
    {
        var hidden = Workspace("hidden", "Private");
        var normalized = new WorkspaceSubject(WorkspaceSubjectType.Topic, "Café");
        var repository = new Mock<IWorkspaceRepository>(MockBehavior.Strict);
        repository.Setup(item => item.LoadEnsureSnapshotAsync(depotId, normalized, default))
            .ReturnsAsync(Snapshot([hidden], [], true, hidden.Workspace.Id));
        var depot = new Mock<ICurrentDepotContext>(); depot.SetupGet(item => item.DepotId).Returns(depotId);
        var safety = new Mock<ISourceSafetyService>();
        var service = new WorkspaceAppService(depot.Object, repository.Object, TimeProvider.System, safety.Object,
            new WorkspaceWriter(repository.Object, ids.Object, new Mock<IWorkspaceAccessContext>().Object));
        var result = await service.ResolveSubjectAsync(new(WorkspaceSubjectType.Topic, "  Cafe\u0301  "), default);
        Assert.Equal(normalized, result.Subject);
        Assert.Equal(WorkspaceSubjectStatus.Unavailable, result.Status);
        Assert.Null(result.Target);
        safety.Verify(item => item.EnsureSafe("Café"), Times.Once);
    }

    private EnsureWorkspaceCommand Command(string path) => new(subject, path, "Proposed", "Changed");
    private WorkspacePathLookup Workspace(string path, string name = "Existing") => new(
        new Workspace(Guid.CreateVersion7(), depotId, null, name, path.Split('/')[^1], null, now), path);
    private WorkspaceWriteSnapshot Snapshot(IReadOnlyList<WorkspacePathLookup> workspaces, IReadOnlyList<Guid> grants,
        bool canCreate = false, Guid? bound = null) => new(subject,
        bound is null ? null : new WorkspaceSubjectBinding(Guid.CreateVersion7(), depotId, bound.Value, subject.Type, subject.Key, now),
        workspaces, new(Guid.CreateVersion7(), false, true, canCreate, 0, grants));
}
