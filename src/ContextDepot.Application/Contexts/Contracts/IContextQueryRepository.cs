using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Domain.Contexts;

namespace ContextDepot.Application.Contexts.Contracts;

public interface IContextQueryRepository
{
    Task<ContextCaptureEvidence?> FindCaptureEvidenceAsync(Guid contextId, CancellationToken cancellationToken) =>
        Task.FromResult<ContextCaptureEvidence?>(null);

    Task<ContextItem?> FindContextByIdAsync(
        Guid contextId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContextSearchCandidateRecord>> FindLexicalContextCandidatesAsync(
        ContextSearchQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentSearchCandidateRecord>> FindLexicalDocumentCandidatesAsync(
        ContextSearchQuery query,
        CancellationToken cancellationToken);
}
