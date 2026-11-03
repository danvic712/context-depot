using ContextDepot.Application.AgentSessions.Dtos;
using ContextDepot.Application.AgentSessions.Enums;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionCaptureCallBudget
{
    Task<BudgetReservation> TryReserveAsync(CaptureWorkLease lease, ProviderCallKind kind, CancellationToken cancellationToken);
}
