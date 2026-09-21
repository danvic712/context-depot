using ContextDepot.Application.Contexts.Dtos;
using ContextDepot.Application.Contexts.Enums;
using ContextDepot.Domain.Contexts;

namespace ContextDepot.Application.Contexts.Contracts;

public interface IContextRepository
{
    Task<ContextItem?> GetByIdAsync(Guid depotId, Guid contextId, CancellationToken cancellationToken);

    Task<ContextPersistenceResult> SaveKeyedAsync(ContextItem candidate, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ContextPersistenceResult> SaveUnkeyedAsync(ContextItem candidate, UnkeyedDuplicatePolicy duplicatePolicy, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ContextPersistenceResult> SupersedeByIdAsync(ContextItem candidate, Guid targetId, DateTimeOffset now, CancellationToken cancellationToken);

    Task<ContextPersistenceResult> ArchiveAsync(Guid depotId, Guid contextId, DateTimeOffset now, CancellationToken cancellationToken);
}
