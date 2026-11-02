using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionRoutingCandidateRepository
{
    // The lease is required for durable embedding reuse and atomic provider budget reservation.
    Task<RoutingCandidateSet> GetCandidatesAsync(RouteLease lease, AuthorizedWorkspaceSnapshot scope,
        CancellationToken cancellationToken);
}
