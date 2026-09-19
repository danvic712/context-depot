using ContextDepot.Application.Retrieval.Dtos;
using ContextDepot.Domain.Contexts;

namespace ContextDepot.Application.Contexts.Contracts;

public interface IContextQueryRepository
{
    Task<ContextItem?> FindContextByIdAsync(
        Guid ownerId,
        Guid contextId,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ContextSearchCandidateRecord>> FindLexicalContextCandidatesAsync(
        ContextSearchQuery query,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<DocumentSearchCandidateRecord>> FindLexicalDocumentCandidatesAsync(
        ContextSearchQuery query,
        CancellationToken cancellationToken);
}
