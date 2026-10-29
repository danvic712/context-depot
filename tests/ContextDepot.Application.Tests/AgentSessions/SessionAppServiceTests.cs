using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Bootstrap.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.AgentSessions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContextDepot.Application.Tests.AgentSessions;

public sealed class SessionAppServiceTests
{
    private readonly Guid depotId = Guid.CreateVersion7();
    private readonly Guid keyId = Guid.CreateVersion7();
    private readonly Guid workspaceId = Guid.CreateVersion7();
    private readonly Guid sessionId = Guid.CreateVersion7();
    private readonly Mock<ISessionRepository> repository = new();
    private readonly Mock<IWorkspaceAppService> workspaces = new();
    private readonly Mock<IWorkspaceAccessContext> access = new();
    private readonly Mock<IContextBootstrapAppService> bootstrap = new();

    private AgentSession Session => new() { Id = sessionId, DepotId = depotId, AccessKeyId = keyId,
        WorkspaceId = workspaceId, Agent = "codex", ClientSessionId = "client-1", CreatedAt = DateTimeOffset.UtcNow };

    private SessionAppService Create(bool enabled = true)
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(d => d.DepotId).Returns(depotId);
        access.SetupGet(a => a.DepotAccessKeyId).Returns(keyId);
        access.Setup(a => a.CanAccess(workspaceId)).Returns(true);
        var workspace = new WorkspaceModel(workspaceId, depotId, "project", "Project", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow);
        workspaces.Setup(w => w.ResolveAsync("project", It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        workspaces.Setup(w => w.GetAsync(workspaceId, It.IsAny<CancellationToken>())).ReturnsAsync(workspace);
        repository.Setup(r => r.GetAsync(depotId, keyId, sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(Session);
        var ids = new Mock<IIdGenerator>(); ids.Setup(i => i.NewId()).Returns(sessionId);
        return new(Options.Create(new SessionCaptureOptions { Enabled = enabled }), depot.Object, access.Object,
            workspaces.Object, repository.Object, bootstrap.Object, ids.Object, TimeProvider.System);
    }

    [Fact]
    public async Task Start_persists_a_server_owned_identity_without_capturing_or_recalling()
    {
        var service = Create();
        AgentSession? stored = null;
        repository.Setup(r => r.StartAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()))
            .Callback<AgentSession, CancellationToken>((s, _) => stored = s).ReturnsAsync((AgentSession s, CancellationToken _) => s);
        var result = await service.StartAsync(new("codex", "client-1", "project"), default);
        Assert.Equal("started", result.Status);
        Assert.Equal("active", result.Session!.Status);
        Assert.Equal(sessionId, result.Session.Id);
        Assert.Equal(keyId, stored!.AccessKeyId);
        Assert.Equal(depotId, stored.DepotId);
        Assert.Equal(workspaceId, stored.WorkspaceId);
        Assert.Null(stored.EndedAt);
        bootstrap.VerifyNoOtherCalls();
        repository.Verify(r => r.EndAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Disabled_start_is_side_effect_free_but_status_remains_available()
    {
        var service = Create(false);
        Assert.Equal("disabled", (await service.StartAsync(new("codex", "client-1", "project"), default)).Status);
        repository.VerifyNoOtherCalls(); workspaces.VerifyNoOtherCalls(); bootstrap.VerifyNoOtherCalls();
        repository.Setup(r => r.CountEventsAsync(depotId, keyId, sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(new SessionEventCounts(1, 0, 0, 0, 1, 0));
        Assert.Equal(1, (await service.StatusAsync(sessionId, null, default)).Events.Pending);
    }

    [Fact]
    public async Task Recall_is_exactly_scoped_and_does_not_mutate_the_session()
    {
        var service = Create();
        var ended = Session; ended.EndedAt = DateTimeOffset.UtcNow; ended.EndReason = "completed";
        repository.Setup(r => r.GetAsync(depotId, keyId, sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(ended);
        BootstrapRequest? query = null;
        bootstrap.Setup(b => b.BootstrapAsync(It.IsAny<BootstrapRequest>(), It.IsAny<CancellationToken>()))
            .Callback<BootstrapRequest, CancellationToken>((r, _) => query = r).ReturnsAsync((BootstrapResult)null!);
        var result = await service.RecallAsync(sessionId, "database", 700, default);
        Assert.Equal("ended", result.Session.Status);
        Assert.Equal(["project"], query!.Workspaces);
        Assert.Equal("database", query.Query); Assert.Equal(700, query.MaxTokens);
        repository.Verify(r => r.StartAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Never);
        repository.Verify(r => r.EndAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task End_only_closes_metadata_without_retrieval_or_queue_processing()
    {
        var service = Create(false);
        var ended = Session; SessionLifecyclePolicy.End(ended, DateTimeOffset.UtcNow, "completed");
        repository.Setup(r => r.EndAsync(depotId, keyId, sessionId, It.IsAny<DateTimeOffset>(), "completed", It.IsAny<CancellationToken>())).ReturnsAsync(ended);
        Assert.Equal("ended", (await service.EndAsync(sessionId, "completed", default)).Status);
        bootstrap.VerifyNoOtherCalls();
        repository.Verify(r => r.CountEventsAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Status_cannot_read_a_receipt_from_another_session()
    {
        var service = Create();
        var receipt = Guid.CreateVersion7();
        repository.Setup(r => r.CountEventsAsync(depotId, keyId, sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(new SessionEventCounts(0, 0, 0, 0, 0, 0));
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.StatusAsync(sessionId, receipt, default));
        Assert.Equal(ApplicationErrorCodes.SessionUnavailable, error.ErrorCode);
        bootstrap.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Unavailable_or_ungranted_session_cannot_recall_or_end()
    {
        var service = Create();
        access.Setup(a => a.CanAccess(workspaceId)).Returns(false);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.RecallAsync(sessionId, "database", 700, default));
        Assert.Equal(ApplicationErrorCodes.SessionUnavailable, error.ErrorCode);
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.EndAsync(sessionId, "completed", default));
        bootstrap.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("")]
    [InlineData("bad\n")]
    public async Task Invalid_start_correlation_is_rejected_before_repository(string clientSessionId)
    {
        var service = Create();
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.StartAsync(new("codex", clientSessionId, "project"), default));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public void Ending_is_idempotent_and_never_reopens_or_replaces_the_first_reason()
    {
        var session = Session;
        var first = DateTimeOffset.UtcNow;
        SessionLifecyclePolicy.End(session, first, "other");
        SessionLifecyclePolicy.End(session, first.AddHours(1), "completed");
        Assert.Equal(first, session.EndedAt); Assert.Equal("other", session.EndReason);
        var error = Assert.Throws<ContextDepotApplicationException>(() => SessionLifecyclePolicy.EnsureNewCaptureAllowed(session));
        Assert.Equal(ApplicationErrorCodes.SessionClosed, error.ErrorCode);
        Assert.Throws<ContextDepotApplicationException>(() => SessionLifecyclePolicy.ValidateEndReason("free text"));
    }
}
