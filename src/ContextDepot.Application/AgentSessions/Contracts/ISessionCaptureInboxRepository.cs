using ContextDepot.Application.AgentSessions.Dtos;

namespace ContextDepot.Application.AgentSessions.Contracts;

public interface ISessionCaptureInboxRepository
{
    Task<SessionCaptureReceipt> AcceptAsync(AuthorizedCaptureEnvelope envelope, CancellationToken cancellationToken);
    Task<IReadOnlyList<RouteLocator>> FindRoutingCandidatesAsync(int limit, CancellationToken cancellationToken);
    Task<RouteLease?> TryClaimRouteAsync(RouteLocator locator, CancellationToken cancellationToken);
    Task<bool> StoreRoutingVectorAsync(RouteLease lease, string encryptedVector, string hash, string fingerprint, CancellationToken cancellationToken);
    Task<bool> CompleteRouteAsync(RouteLease lease, RouteDecision decision, CancellationToken cancellationToken);
    Task<bool> PromoteAsync(RouteLocator locator, CancellationToken cancellationToken);
    Task<IReadOnlyList<RouteLocator>> FindPromotableAsync(int limit, CancellationToken cancellationToken);
    Task<int> CleanupAsync(int limit, CancellationToken cancellationToken);
}
