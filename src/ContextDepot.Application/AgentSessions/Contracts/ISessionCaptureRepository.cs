using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;
using ContextDepot.Domain.AgentSessions;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionCaptureRepository
{
    Task<SessionCaptureReceipt> EnqueueAsync(SessionCaptureEvent message, CancellationToken cancellationToken);
    Task<SessionCaptureReceipt> EnqueueTurnAsync(SessionCaptureEvent user, SessionCaptureEvent assistant, CancellationToken cancellationToken);
    Task CleanupAsync(CancellationToken cancellationToken);
    Task<CapturedTurn?> ClaimAsync(CancellationToken cancellationToken);
    Task FinishAsync(CapturedTurn turn, string resultCode, SessionCaptureDisposition disposition, CancellationToken cancellationToken);
}
