using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.Shared.Exceptions;
using ContextDepot.Domain.AgentSessions;

namespace ContextDepot.Application.AgentSessions;

public static class SessionLifecyclePolicy
{
    public static void ValidateStart(SessionStartRequest request)
    {
        if (!SessionCaptureProtocol.ValidAgent(request.Agent) || !SessionCaptureProtocol.ValidId(request.ClientSessionId) ||
            string.IsNullOrWhiteSpace(request.Workspace) || request.Workspace.Length > 500)
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSessionCapture);
    }

    public static void End(AgentSession session, DateTimeOffset now, string reason)
    {
        ValidateEndReason(reason);
        if (session.EndedAt is not null) return;
        session.EndedAt = now;
        session.EndReason = reason;
    }

    public static void ValidateEndReason(string reason)
    {
        if (reason is not ("completed" or "interrupted" or "other"))
            throw new ContextDepotApplicationException(ApplicationErrorCodes.InvalidSessionCapture);
    }

    public static void EnsureNewCaptureAllowed(AgentSession session)
    {
        if (session.EndedAt is not null) throw new ContextDepotApplicationException(ApplicationErrorCodes.SessionClosed);
    }
}
