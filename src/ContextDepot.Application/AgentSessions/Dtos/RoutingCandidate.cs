namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record RoutingCandidate(RoutingWorkspace Workspace, bool ExactPath, bool ExactName,
    int MetadataMatches, double MaxSimilarity)
{
    public bool HasSupport => ExactPath || ExactName || MetadataMatches >= 2 || MaxSimilarity >= 0.82;
}
