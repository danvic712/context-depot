using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionWorkspaceRouter
{
    Task<RouteDecision> RouteAsync(RouteLease lease, AuthorizedWorkspaceSnapshot scope, CancellationToken cancellationToken);
}
