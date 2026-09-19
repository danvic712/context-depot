using ContextDepot.Application.SemanticRetrieval.Dtos;

namespace ContextDepot.Application.SemanticRetrieval.Contracts;

public interface ISemanticRetrievalRepository
{
    Task<IReadOnlyList<SemanticContextCandidateRecord>> FindContextCandidatesAsync(
        SemanticCandidateQuery query,
        ReadOnlyMemory<float> queryVector,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<SemanticDocumentCandidateRecord>> FindDocumentCandidatesAsync(
        SemanticCandidateQuery query,
        ReadOnlyMemory<float> queryVector,
        CancellationToken cancellationToken);
}
