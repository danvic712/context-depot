using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionAppService
{
    Task<SessionStartResult> StartAsync(SessionStartRequest request, CancellationToken cancellationToken);
    Task<SessionModel> EndAsync(Guid sessionId, string reason, CancellationToken cancellationToken);
    Task<SessionStatusResult> StatusAsync(Guid sessionId, Guid? receiptId, CancellationToken cancellationToken);
    Task<SessionRecallResult> RecallAsync(Guid sessionId, string query, int maxTokens, CancellationToken cancellationToken);
}
