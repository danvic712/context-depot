using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Domain.Depots;

namespace ContextDepot.Application.Settings.Contracts;

public interface IAccessKeyRepository
{
    Task<AccessKeyListDto> ListAsync(Guid depotId, CancellationToken cancellationToken);
    Task<bool> WorkspacesBelongToDepotAsync(Guid depotId, IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    Task<AccessKeyDto> CreateAsync(DepotAccessKey key, CancellationToken cancellationToken);
    Task<AccessKeyDto?> RevokeAsync(Guid depotId, Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task<AccessKeyDto?> RotateAsync(Guid depotId, Guid id, GeneratedAccessKey secret, Guid replacementId,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<AccessKeyDto?> SetGrantsAsync(Guid depotId, Guid id, IReadOnlyList<Guid> ids,
        DateTimeOffset now, CancellationToken cancellationToken);
}
