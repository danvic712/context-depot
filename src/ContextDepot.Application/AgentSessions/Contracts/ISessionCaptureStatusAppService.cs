using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionCaptureStatusAppService
{
    Task<SessionCaptureStatusResult> GetAsync(SessionCaptureStatusRequest request, CancellationToken cancellationToken);
}
