using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.Bootstrap.Contracts;

public interface IBootstrapRepository
{
    Task<IReadOnlyList<BootstrapWorkspaceCandidate>> FindScopeCandidatesAsync(
        BootstrapQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BootstrapContextCandidate>> FindContextCandidatesAsync(
        BootstrapQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BootstrapDocumentChunkCandidate>> FindDocumentCandidatesAsync(
        BootstrapQuery query,
        CancellationToken cancellationToken = default);
}
