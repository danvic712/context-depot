using ContextDepot.Application.Contexts.Dtos;

namespace ContextDepot.Application.Contexts.Contracts;

public interface IContextAppService
{
    Task<SaveContextResult> SaveAsync(SaveContextCommand command, CancellationToken cancellationToken);

    Task ArchiveAsync(Guid contextId, CancellationToken cancellationToken);
}
