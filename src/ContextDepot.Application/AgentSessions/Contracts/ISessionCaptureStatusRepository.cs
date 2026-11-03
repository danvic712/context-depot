using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionCaptureStatusRepository
{
    Task<SessionCaptureStatusResult?> GetAsync(Guid depotId, Guid keyId, SessionCaptureStatusRequest request, CancellationToken cancellationToken);
}
