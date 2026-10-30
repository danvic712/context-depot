using System.Security.Cryptography;
using System.Text;
using ContextDepot.Application.AgentSessions.Contracts;
using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Application.Shared.Runtime.Contracts;
using ContextDepot.Application.Workspaces.Contracts;
using ContextDepot.Domain.AgentSessions;
using Microsoft.Extensions.Options;

namespace ContextDepot.Application.AgentSessions;

public sealed class SessionCaptureAppService(IOptions<SessionCaptureOptions> options,
    ICurrentDepotContext depot, IWorkspaceAccessContext access, IWorkspaceAppService workspaces,
    ISessionCaptureRepository repository, ISessionContentProtector protector, IIdGenerator ids,
    TimeProvider clock, ISessionRepository sessions) : ISessionCaptureAppService
{
    public async Task<SessionCaptureReceipt> CaptureAsync(SessionCaptureRequest request, CancellationToken cancellationToken)
    {
        if (!options.Value.Enabled) return new("disabled");
        var capture = SessionCaptureProtocol.Normalize(request);
        if (access.DepotAccessKeyId is not Guid keyId || depot.DepotId == Guid.Empty)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceUnavailable);
        var workspace = await workspaces.ResolveAsync(request.Workspace, cancellationToken);
        if (workspace is null || workspace.DepotId != depot.DepotId || workspace.Path != request.Workspace || !access.CanAccess(workspace.Id))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.WorkspaceUnavailable);
        if (capture.Messages.Count == 1 && string.IsNullOrWhiteSpace(capture.Messages[0].Content))
            return new("skipped");
        if (request.SessionId is Guid sessionId)
        {
            var session = await sessions.GetAsync(depot.DepotId, keyId, sessionId, cancellationToken);
            if (session is null || session.WorkspaceId != workspace.Id || session.Agent != request.Agent ||
                session.ClientSessionId != capture.ClientSessionId)
                throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionUnavailable);
        }
        var now = clock.GetUtcNow();
        var messages = capture.Messages.Select((message, index) => new SessionCaptureEvent
        {
            Id = ids.NewId(), SessionId = request.SessionId, DepotId = depot.DepotId, AccessKeyId = keyId, WorkspaceId = workspace.Id,
            Agent = request.Agent, ClientSessionId = capture.ClientSessionId, TurnId = capture.TurnId,
            EventId = capture.EventIds[index], Phase = message.Role, EncryptedContent = protector.Protect(message.Content!),
            ContentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(message.Content!))),
            CreatedAt = now, NextAttemptAt = now
        }).ToArray();
        return messages.Length == 1
            ? await repository.EnqueueAsync(messages[0], cancellationToken)
            : await repository.EnqueueTurnAsync(messages[0], messages[1], cancellationToken);
    }
}
