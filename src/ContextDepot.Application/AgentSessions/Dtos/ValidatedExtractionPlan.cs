namespace ContextDepot.Application.AgentSessions.Dtos;

public sealed record ValidatedExtractionPlan(string Version, IReadOnlyList<ValidatedMemoryCandidate> Candidates)
{
    public const string CurrentVersion = "capture-summary-v1";
}
