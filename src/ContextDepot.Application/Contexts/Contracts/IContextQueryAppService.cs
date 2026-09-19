using ContextDepot.Application.Contexts.Dtos;

namespace ContextDepot.Application.Contexts.Contracts;

public interface IContextQueryAppService
{
    Task<ContextSearchResult> SearchAsync(
        ContextSearchRequest request,
        CancellationToken cancellationToken);

    Task<ContextDetailModel?> GetAsync(
        Guid contextId,
        CancellationToken cancellationToken);
}
