using ContextDepot.Application.Workspaces.Enums;
using ContextDepot.Application.AgentContext.Enums;
using System.Text;
using System.Text.Json;
using ContextDepot.Application.AgentContext;
using ContextDepot.Application.AgentContext.Dtos;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Bootstrap.Enums;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Shared.Safety.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.Workspaces;
using Moq;

namespace ContextDepot.Application.Tests.AgentContext;

public sealed class AgentContextTests
{
    private readonly Mock<IContextBootstrapAppService> bootstrap = new(MockBehavior.Strict);
    private readonly Mock<IWorkspaceAppService> workspaces = new(MockBehavior.Strict);
    private readonly Mock<IWorkspaceAccessContext> access = new();
    private readonly Mock<IIdGenerator> ids = new();
    private readonly WorkspaceSubject subject = new(WorkspaceSubjectType.Project, "project-key");
    private static readonly BootstrapResult Empty = new(new(ScopeResolutionStatus.Broad, []), [], [], new(false, false, "lexical"), 0);
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    public AgentContextTests()
    {
        ids.Setup(item => item.NewId()).Returns(() => Guid.CreateVersion7());
        access.SetupGet(item => item.AccessVersion).Returns(9007199254740993L);
    }

    [Theory]
    [InlineData("")]
    [InlineData("谢谢！")]
    [InlineData("OK.")]
    public async Task Automatic_acknowledgements_skip_every_repository_and_creation_action(string input)
    {
        var result = await Service().PrepareAsync(new(input, Subject: subject, Event: new(AgentContextEventType.TaskInput),
            Workflow: new(true, true)), default);
        Assert.Equal(AgentRetrievalStatus.Skipped, result.Retrieval.Status);
        Assert.Null(result.Retrieval.Result);
        Assert.Equal(WorkspaceSubjectStatus.NotEvaluated, result.Workspace.Status);
        Assert.Null(result.Workspace.CanCreateRootWorkspaces);
        Assert.Empty(result.NextActions);
        bootstrap.VerifyNoOtherCalls(); workspaces.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Explicit_empty_query_preserves_retrieval_semantics_and_a_string_version_receipt()
    {
        bootstrap.Setup(item => item.BootstrapAsync(new("", null, 1200), default)).ReturnsAsync(Empty);
        var result = await Service().PrepareAsync(new(""), default);
        Assert.Equal(AgentRetrievalStatus.Completed, result.Retrieval.Status);
        Assert.Same(Empty, result.Retrieval.Result);
        Assert.Equal(7, result.Receipt.ResultId.Version);
        Assert.Equal("9007199254740993", result.Receipt.AccessVersion);
        Assert.Equal(TimeSpan.Zero, result.Receipt.GeneratedAt.Offset);
        Assert.Empty(result.NextActions);
        workspaces.VerifyNoOtherCalls();
        using var payload = JsonDocument.Parse(JsonSerializer.Serialize(result, json));
        Assert.Equal("contextRequest", payload.RootElement.GetProperty("event").GetProperty("type").GetString());
        Assert.Equal("broad", payload.RootElement.GetProperty("retrieval").GetProperty("result").GetProperty("scopeResolution").GetProperty("status").GetString());
        Assert.Contains("\"Status\":\"broad\"", JsonSerializer.Serialize(Empty));
    }

    [Fact]
    public async Task A_single_lexical_winner_remains_a_candidate_and_cannot_trigger_creation()
    {
        var candidate = Workspace("candidate");
        workspaces.Setup(item => item.ResolveSubjectAsync(subject, default))
            .ReturnsAsync(new WorkspaceSubjectResolution(subject, WorkspaceSubjectStatus.NeedsResolution, null, true));
        workspaces.Setup(item => item.ResolveAsync("candidate", default)).ReturnsAsync(candidate);
        bootstrap.Setup(item => item.BootstrapAsync(new("task", null, 1200), default))
            .ReturnsAsync(Empty with { ScopeResolution = new(ScopeResolutionStatus.Resolved, ["candidate"]) });
        var result = await Service().PrepareAsync(new("task", Subject: subject, Workflow: new(false, true)), default);
        Assert.Null(result.Workspace.Target);
        Assert.Equal(WorkspaceSubjectStatus.NeedsResolution, result.Workspace.Status);
        Assert.Single(result.Workspace.Candidates);
        Assert.Equal(AgentContextActionType.ResolveWorkspace, Assert.Single(result.NextActions).Type);
    }

    [Theory]
    [InlineData(WorkspaceSubjectStatus.NeedsResolution, true, 1)]
    [InlineData(WorkspaceSubjectStatus.NeedsResolution, false, 0)]
    [InlineData(WorkspaceSubjectStatus.Unavailable, true, 0)]
    public async Task Only_a_stable_unbound_subject_with_capability_suggests_Ensure(WorkspaceSubjectStatus status, bool capability, int count)
    {
        workspaces.Setup(item => item.ResolveSubjectAsync(subject, default)).ReturnsAsync(new WorkspaceSubjectResolution(subject, status, null, capability));
        bootstrap.Setup(item => item.BootstrapAsync(new("task", null, 1200), default)).ReturnsAsync(Empty);
        var result = await Service().PrepareAsync(new("task", Subject: subject, Workflow: new(false, true)), default);
        Assert.Equal(count, result.NextActions.Count(item => item.Type == AgentContextActionType.EnsureWorkspace));
        Assert.Null(result.Workspace.Target);
        workspaces.Verify(item => item.EnsureAsync(It.IsAny<EnsureWorkspaceCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Bound_subject_selects_the_actual_path_for_retrieval()
    {
        var target = Workspace("real/path");
        workspaces.Setup(item => item.ResolveSubjectAsync(subject, default)).ReturnsAsync(new WorkspaceSubjectResolution(subject, WorkspaceSubjectStatus.Resolved, target, false));
        bootstrap.Setup(item => item.BootstrapAsync(It.Is<BootstrapRequest>(request => request.Query == "task" && request.Workspaces!.SequenceEqual(new[] { "real/path" })), default)).ReturnsAsync(Empty);
        var result = await Service().PrepareAsync(new("task", Subject: subject, Workflow: new(false, true)), default);
        Assert.Same(target, result.Workspace.Target);
        Assert.Empty(result.NextActions);
    }

    [Fact]
    public async Task Explicit_readable_target_can_be_bound_without_root_creation_capability()
    {
        var target = Workspace("existing");
        workspaces.Setup(item => item.ResolveSubjectAsync(subject, default)).ReturnsAsync(new WorkspaceSubjectResolution(subject, WorkspaceSubjectStatus.NeedsResolution, null, false));
        workspaces.Setup(item => item.ResolveAsync("existing", default)).ReturnsAsync(target);
        bootstrap.Setup(item => item.BootstrapAsync(It.Is<BootstrapRequest>(request => request.Workspaces!.SequenceEqual(new[] { "existing" })), default)).ReturnsAsync(Empty);
        var result = await Service().PrepareAsync(new("task", ["existing"], subject, Workflow: new(false, true)), default);
        Assert.Same(target, result.Workspace.Target);
        Assert.Equal(AgentContextActionType.EnsureWorkspace, Assert.Single(result.NextActions).Type);
        Assert.Equal("explicitTargetNeedsBinding", result.NextActions[0].ReasonCode);
    }

    [Fact]
    public async Task Long_automatic_query_keeps_both_ends_and_never_splits_surrogate_pairs()
    {
        var input = new string('a', 2998) + "😀" + new string('b', 5000) + "😀" + new string('c', 2999);
        BootstrapRequest? captured = null;
        bootstrap.Setup(item => item.BootstrapAsync(It.IsAny<BootstrapRequest>(), default)).Callback<BootstrapRequest, CancellationToken>((request, _) => captured = request).ReturnsAsync(Empty);
        await Service().PrepareAsync(new(input, Event: new(AgentContextEventType.TaskInput)), default);
        Assert.InRange(captured!.Query.Length, 5996, 6000);
        Assert.StartsWith("aaaa", captured.Query);
        Assert.EndsWith("cccc", captured.Query);
        new UTF8Encoding(false, true).GetBytes(captured.Query);
    }

    [Fact]
    public async Task Retrieval_failures_and_cancellation_propagate_without_success_receipts()
    {
        bootstrap.Setup(item => item.BootstrapAsync(new("task", null, 1200), default)).ThrowsAsync(new InvalidOperationException("offline"));
        await Assert.ThrowsAsync<InvalidOperationException>(() => Service().PrepareAsync(new("task"), default));
        ids.Verify(item => item.NewId(), Times.Never);
        using var cancel = new CancellationTokenSource(); cancel.Cancel();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => Service().PrepareAsync(new("task"), cancel.Token));
        bootstrap.Verify(item => item.BootstrapAsync(It.IsAny<BootstrapRequest>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("context_save", true, AgentRetrievalStatus.Skipped, 0)]
    [InlineData("shell", false, AgentRetrievalStatus.Skipped, 0)]
    [InlineData("shell", true, AgentRetrievalStatus.NotRequested, 1)]
    public void Activity_is_pure_and_canonical_operations_prevent_reminder_loops(string tool, bool capture, AgentRetrievalStatus status, int actions)
    {
        var result = Service().AssessActivity(new(new(tool, AgentOperationStatus.Failed), Workflow: new(capture)));
        Assert.Equal(status, result.Retrieval.Status);
        Assert.Null(result.Retrieval.Result);
        Assert.Equal(WorkspaceSubjectStatus.NotEvaluated, result.Workspace.Status);
        Assert.Null(result.Workspace.CanCreateRootWorkspaces);
        Assert.Equal(actions, result.NextActions.Count);
        if (actions > 0) Assert.Equal(AgentContextActionType.AssessCapture, result.NextActions[0].Type);
        bootstrap.VerifyNoOtherCalls(); workspaces.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("1")]
    [InlineData("\"taskInput, contextRequest\"")]
    [InlineData("\"undefined\"")]
    public void Public_event_enum_rejects_numeric_combined_and_undefined_values(string value) =>
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentContextEvent>("{\"type\":" + value + "}", json));

    [Fact]
    public void Declared_subject_and_event_require_their_type_in_json()
    {
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkspaceSubject>("{\"key\":\"project\"}", json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<WorkspaceSubject>("{\"type\":\"project\"}", json));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<AgentContextEvent>("{}", json));
    }

    [Fact]
    public async Task Invalid_events_metadata_and_budgets_fail_before_retrieval()
    {
        foreach (var request in new[] { new PrepareAgentContextRequest("task", Event: new(AgentContextEventType.OperationCompleted)),
            new PrepareAgentContextRequest("task", Event: new(EventId: new string('a', 201))), new PrepareAgentContextRequest("task", MaxTokens: 0) })
            await Assert.ThrowsAsync<ContextDepotApplicationException>(() => Service().PrepareAsync(request, default));
        Assert.Throws<ContextDepotApplicationException>(() => Service().AssessActivity(new(new(" "))));
        bootstrap.VerifyNoOtherCalls(); workspaces.VerifyNoOtherCalls();
    }

    private AgentContextAppService Service() => new(bootstrap.Object, workspaces.Object, access.Object,
        new Mock<ISourceSafetyService>().Object, ids.Object, TimeProvider.System);
    private static WorkspaceModel Workspace(string path) => new(Guid.CreateVersion7(), Guid.CreateVersion7(), path,
        "Workspace", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
}
