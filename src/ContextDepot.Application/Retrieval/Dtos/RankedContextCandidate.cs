using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.Retrieval.Dtos;

public sealed record RankedContextCandidate(
    BootstrapContextCandidate Candidate,
    double ExactScore,
    double LexicalScore,
    double SemanticScore,
    double QualityScore,
    double VerificationScore,
    double Score)
{
    public BootstrapContextCandidate Context => Candidate;

    public bool IsExactMatch => ExactScore > 0;

    public Guid SourceIdentity => Candidate.Id;
}
