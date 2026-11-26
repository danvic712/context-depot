using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Domain.AgentSessions;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionRepository
{
    Task<AgentSession> StartAsync(AgentSession session, CancellationToken cancellationToken);
    Task<AgentSession?> GetAsync(Guid accessKeyId, Guid sessionId, CancellationToken cancellationToken);
    Task<AgentSession> EndAsync(Guid accessKeyId, Guid sessionId, DateTimeOffset now,
        string reason, CancellationToken cancellationToken);
    Task<SessionEventCounts> CountEventsAsync(Guid accessKeyId, Guid sessionId, CancellationToken cancellationToken);
    Task<SessionReceiptStatus?> GetReceiptAsync(Guid accessKeyId, Guid sessionId, Guid receiptId,
        CancellationToken cancellationToken);
}
