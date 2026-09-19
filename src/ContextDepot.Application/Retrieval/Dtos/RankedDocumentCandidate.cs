using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.Retrieval.Dtos;

public sealed record RankedDocumentCandidate(
    BootstrapDocumentChunkCandidate Candidate,
    double ExactScore,
    double LexicalScore,
    double SemanticScore,
    double QualityScore,
    double VerificationScore,
    double Score)
{
    public BootstrapDocumentChunkCandidate Document => Candidate;

    public bool IsExactMatch => ExactScore > 0;

    public Guid SourceIdentity => Candidate.Id;
}
