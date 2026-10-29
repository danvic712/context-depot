using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Bootstrap.Contracts;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Domain.AgentSessions;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.AgentSessions;

public sealed class SessionAppService(IOptions<SessionCaptureOptions> options, ICurrentDepotContext depot,
    IWorkspaceAccessContext access, IWorkspaceAppService workspaces, ISessionRepository repository,
    IContextBootstrapAppService bootstrap, IIdGenerator ids, TimeProvider clock) : ISessionAppService
{
    public async Task<SessionStartResult> StartAsync(SessionStartRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return new("disabled", null);
        SessionLifecyclePolicy.ValidateStart(request);
        var keyId = CurrentKey();
        var workspace = await workspaces.ResolveAsync(request.Workspace, cancellationToken);
        if (workspace is null || workspace.DepotId != depot.DepotId || workspace.Path != request.Workspace || !access.CanAccess(workspace.Id))
            throw Unavailable();
        var candidate = new AgentSession { Id = ids.NewId(), DepotId = depot.DepotId, AccessKeyId = keyId,
            WorkspaceId = workspace.Id, Agent = request.Agent, ClientSessionId = request.ClientSessionId,
            CreatedAt = clock.GetUtcNow() };
        var stored = await repository.StartAsync(candidate, cancellationToken);
        return new(stored.Id == candidate.Id ? "started" : "existing", Model(stored, workspace.Path));
    }

    public async Task<SessionModel> EndAsync(Guid sessionId, string reason, CancellationToken cancellationToken)
    {
        SessionLifecyclePolicy.ValidateEndReason(reason);
        var (session, path) = await ResolveAsync(sessionId, cancellationToken);
        var ended = await repository.EndAsync(depot.DepotId, CurrentKey(), session.Id, clock.GetUtcNow(), reason, cancellationToken);
        return Model(ended, path);
    }

    public async Task<SessionStatusResult> StatusAsync(Guid sessionId, Guid? receiptId, CancellationToken cancellationToken)
    {
        var (session, path) = await ResolveAsync(sessionId, cancellationToken);
        var counts = await repository.CountEventsAsync(depot.DepotId, CurrentKey(), sessionId, cancellationToken);
        var receipt = receiptId is Guid id
            ? await repository.GetReceiptAsync(depot.DepotId, CurrentKey(), sessionId, id, cancellationToken) : null;
        if (receiptId.HasValue && receipt is null) throw Unavailable();
        return new(Model(session, path), counts, receipt);
    }

    public async Task<SessionRecallResult> RecallAsync(Guid sessionId, string query, int maxTokens, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Length > SessionCaptureOptions.MaxContentLength || maxTokens is < 1 or > 8000)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSessionCapture);
        var (session, path) = await ResolveAsync(sessionId, cancellationToken);
        var context = await bootstrap.BootstrapAsync(new(query, [path], maxTokens), cancellationToken);
        return new(Model(session, path), context);
    }

    private async Task<(AgentSession Session, string Path)> ResolveAsync(Guid sessionId, CancellationToken cancellationToken)
    {
        if (sessionId == Guid.Empty) throw Unavailable();
        var session = await repository.GetAsync(depot.DepotId, CurrentKey(), sessionId, cancellationToken);
        if (session is null || !access.CanAccess(session.WorkspaceId)) throw Unavailable();
        var workspace = await workspaces.GetAsync(session.WorkspaceId, cancellationToken);
        if (workspace is null || workspace.DepotId != depot.DepotId) throw Unavailable();
        return (session, workspace.Path);
    }

    private Guid CurrentKey() => depot.DepotId != Guid.Empty && access.DepotAccessKeyId is Guid keyId ? keyId : throw Unavailable();
    private static ContextDepotApplicationException Unavailable() => new(ApplicationErrorCodes.SessionUnavailable);
    private static SessionModel Model(AgentSession session, string path) => new(session.Id, session.Agent,
        session.ClientSessionId, path, session.EndedAt is null ? "active" : "ended", session.CreatedAt, session.EndedAt, session.EndReason);
}
