namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record RoutingCandidateSet(IReadOnlyList<RoutingCandidate> Candidates,
    string ResolverVersion = "workspace-router-v1", RouteDecision? Outcome = null);
