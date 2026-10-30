using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionCaptureAppService
{
    Task<SessionCaptureReceipt> CaptureAsync(SessionCaptureRequest request, CancellationToken cancellationToken);
}
