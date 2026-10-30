using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Application.Workspaces.Dtos;
using ContextDepot.Domain.AgentSessions;
using Microsoft.Extensions.Options;
using Moq;

namespace ContextDepot.Application.Tests.AgentSessions;

public sealed class SessionCaptureAppServiceTests
{
    private readonly Guid depotId = Guid.CreateVersion7();
    private readonly Guid workspaceId = Guid.CreateVersion7();
    private readonly Guid keyId = Guid.CreateVersion7();
    private readonly Mock<IWorkspaceAppService> workspaces = new();
    private readonly Mock<IWorkspaceAccessContext> access = new();
    private readonly Mock<ISessionCaptureRepository> repository = new();
    private readonly Mock<ISessionRepository> sessions = new();
    private readonly Mock<ISessionContentProtector> protector = new();
    private static SessionCaptureRequest Request => new(1, "codex", "session-1", "turn-1", "user", "event-1", "project", "我决定在第一版使用 PostgreSQL 数据库。");

    private SessionCaptureAppService Create(bool enabled = true)
    {
        var depot = new Mock<ICurrentDepotContext>();
        depot.SetupGet(x => x.DepotId).Returns(depotId);
        access.SetupGet(x => x.DepotAccessKeyId).Returns(keyId);
        access.Setup(x => x.CanAccess(workspaceId)).Returns(true);
        workspaces.Setup(x => x.ResolveAsync("project", It.IsAny<CancellationToken>())).ReturnsAsync(
            new WorkspaceModel(workspaceId, depotId, "project", "Project", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        var ids = new Mock<IIdGenerator>();
        ids.Setup(x => x.NewId()).Returns(() => Guid.CreateVersion7());
        protector.Setup(x => x.Protect(It.IsAny<string>())).Returns("encrypted");
        return new(Options.Create(new SessionCaptureOptions { Enabled = enabled }), depot.Object, access.Object,
            workspaces.Object, repository.Object, protector.Object, ids.Object, TimeProvider.System, sessions.Object);
    }

    [Fact]
    public async Task Disabled_does_not_touch_dependencies()
    {
        var receipt = await Create(false).CaptureAsync(Request, default);
        Assert.Equal("disabled", receipt.Status);
        workspaces.VerifyNoOtherCalls();
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Agent_label_is_source_metadata_and_does_not_select_a_different_capture_workflow()
    {
        SessionCaptureEvent? persisted = null;
        repository.Setup(x => x.EnqueueAsync(It.IsAny<SessionCaptureEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SessionCaptureEvent, CancellationToken>((message, _) => persisted = message)
            .ReturnsAsync(new SessionCaptureReceipt("accepted", Guid.CreateVersion7()));
        var result = await Create().CaptureAsync(Request with { Agent = "declared-client" }, default);
        Assert.Equal("accepted", result.Status);
        Assert.NotNull(persisted);
        Assert.Equal("declared-client", persisted.Agent);
        Assert.Equal(depotId, persisted.DepotId);
        Assert.Equal(keyId, persisted.AccessKeyId);
    }

    [Theory]
    [InlineData("agent")]
    [InlineData("schema")]
    [InlineData("phase")]
    [InlineData("id")]
    [InlineData("length")]
    public async Task Invalid_envelopes_are_rejected_before_persistence(string field)
    {
        var request = field switch
        {
            "agent" => Request with { Agent = "invalid\nagent" },
            "schema" => Request with { SchemaVersion = 3 },
            "phase" => Request with { Phase = "system" },
            "id" => Request with { TurnId = "bad\nturn" },
            _ => Request with { Content = new string('x', 20_001) }
        };
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => Create().CaptureAsync(request, default));
        Assert.Equal(ApplicationErrorCodes.InvalidSessionCapture, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Authorized_event_is_encrypted_and_uses_server_identity()
    {
        SessionCaptureEvent? persisted = null;
        repository.Setup(x => x.EnqueueAsync(It.IsAny<SessionCaptureEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SessionCaptureEvent, CancellationToken>((e, _) => persisted = e)
            .ReturnsAsync(new SessionCaptureReceipt("accepted", Guid.CreateVersion7()));
        var result = await Create().CaptureAsync(Request, default);
        Assert.Equal("accepted", result.Status);
        Assert.NotNull(persisted);
        Assert.Equal(depotId, persisted.DepotId);
        Assert.Equal(keyId, persisted.AccessKeyId);
        Assert.Equal(workspaceId, persisted.WorkspaceId);
        Assert.Equal("encrypted", persisted.EncryptedContent);
        Assert.Equal(64, persisted.ContentHash.Length);
        protector.Verify(x => x.Protect(Request.Content!), Times.Once);
    }

    [Fact]
    public async Task Empty_stop_is_skipped_without_queueing()
    {
        var result = await Create().CaptureAsync(Request with { Phase = "assistant", Content = null }, default);
        Assert.Equal("skipped", result.Status);
        repository.VerifyNoOtherCalls();
        protector.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Native_and_no_hook_turns_use_one_atomic_queue_operation_with_server_owned_identity()
    {
        SessionCaptureEvent? user = null;
        SessionCaptureEvent? assistant = null;
        repository.Setup(x => x.EnqueueTurnAsync(It.IsAny<SessionCaptureEvent>(), It.IsAny<SessionCaptureEvent>(),
                It.IsAny<CancellationToken>()))
            .Callback<SessionCaptureEvent, SessionCaptureEvent, CancellationToken>((u, a, _) => (user, assistant) = (u, a))
            .ReturnsAsync(new SessionCaptureReceipt("accepted", Guid.CreateVersion7()));
        var request = new SessionCaptureRequest(2, "mcp-agent", null, null, null, "completed-turn", "project", null,
            "turn", [new("user", Request.Content), new("assistant", "已整理到方案中。")]);
        var result = await Create().CaptureAsync(request, default);
        Assert.Equal("accepted", result.Status);
        Assert.NotNull(user);
        Assert.NotNull(assistant);
        Assert.NotEqual(user.Id, assistant.Id);
        Assert.Equal(user.ClientSessionId, assistant.ClientSessionId);
        Assert.Equal(user.TurnId, assistant.TurnId);
        Assert.NotEqual(user.EventId, assistant.EventId);
        Assert.Equal(workspaceId, user.WorkspaceId);
        Assert.Equal(workspaceId, assistant.WorkspaceId);
        Assert.Equal(keyId, assistant.AccessKeyId);
        Assert.Equal("encrypted", assistant.EncryptedContent);
        Assert.Equal("mcp-agent", assistant.Agent);
        repository.Verify(x => x.EnqueueAsync(It.IsAny<SessionCaptureEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Empty_v2_message_skips_but_incomplete_turn_rejects_without_persisting()
    {
        var request = new SessionCaptureRequest(2, "plugin", "session", "turn", null, "empty", "project", null,
            "message", [new("assistant", null)]);
        var service = Create();
        Assert.Equal("skipped", (await service.CaptureAsync(request, default)).Status);
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.CaptureAsync(request with
        {
            Mode = "turn", Messages = [new("user", "实际可见的用户陈述。"), new("assistant", null)]
        }, default));
        repository.VerifyNoOtherCalls();
        protector.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Navigable_but_ungranted_workspace_is_rejected()
    {
        var service = Create();
        access.Setup(x => x.CanAccess(workspaceId)).Returns(false);
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.CaptureAsync(Request, default));
        Assert.Equal(ApplicationErrorCodes.WorkspaceUnavailable, error.ErrorCode);
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Noncanonical_workspace_path_is_rejected()
    {
        var service = Create();
        workspaces.Setup(x => x.ResolveAsync("project/", It.IsAny<CancellationToken>())).ReturnsAsync(
            new WorkspaceModel(workspaceId, depotId, "project", "Project", null, "{}", DateTimeOffset.UtcNow, DateTimeOffset.UtcNow));
        await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.CaptureAsync(Request with { Workspace = "project/" }, default));
        repository.VerifyNoOtherCalls();
    }

    [Fact]
    public async Task Managed_capture_binds_to_the_existing_session_without_starting_or_ending_it()
    {
        var service = Create();
        var sessionId = Guid.CreateVersion7();
        sessions.Setup(s => s.GetAsync(depotId, keyId, sessionId, It.IsAny<CancellationToken>())).ReturnsAsync(
            new AgentSession { Id = sessionId, DepotId = depotId, AccessKeyId = keyId, WorkspaceId = workspaceId,
                Agent = "codex", ClientSessionId = "session-1" });
        SessionCaptureEvent? stored = null;
        repository.Setup(r => r.EnqueueAsync(It.IsAny<SessionCaptureEvent>(), It.IsAny<CancellationToken>()))
            .Callback<SessionCaptureEvent, CancellationToken>((e, _) => stored = e).ReturnsAsync(new SessionCaptureReceipt("accepted", Guid.CreateVersion7()));
        Assert.Equal("accepted", (await service.CaptureAsync(Request with { SessionId = sessionId }, default)).Status);
        Assert.Equal(sessionId, stored!.SessionId);
        sessions.Verify(s => s.StartAsync(It.IsAny<AgentSession>(), It.IsAny<CancellationToken>()), Times.Never);
        sessions.Verify(s => s.EndAsync(It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<Guid>(), It.IsAny<DateTimeOffset>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("missing")]
    [InlineData("workspace")]
    [InlineData("agent")]
    [InlineData("client")]
    public async Task Managed_capture_rejects_missing_or_mismatched_session(string mismatch)
    {
        var service = Create();
        var id = Guid.CreateVersion7();
        if (mismatch != "missing") sessions.Setup(s => s.GetAsync(depotId, keyId, id, It.IsAny<CancellationToken>())).ReturnsAsync(
            new AgentSession { Id = id, DepotId = depotId, AccessKeyId = keyId,
                WorkspaceId = mismatch == "workspace" ? Guid.CreateVersion7() : workspaceId,
                Agent = mismatch == "agent" ? "other" : "codex", ClientSessionId = mismatch == "client" ? "other" : "session-1" });
        var error = await Assert.ThrowsAsync<ContextDepotApplicationException>(() => service.CaptureAsync(Request with { SessionId = id }, default));
        Assert.Equal(ApplicationErrorCodes.SessionUnavailable, error.ErrorCode);
        repository.VerifyNoOtherCalls(); protector.VerifyNoOtherCalls();
    }
}
