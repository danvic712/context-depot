using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Domain.Access;

namespace ContextDepot.Application.Settings.Contracts;

public interface IAccessKeyRepository
{
    Task<AccessKeyListDto> ListAsync(CancellationToken cancellationToken);
    Task<bool> WorkspacesExistAsync(IReadOnlyList<Guid> ids, CancellationToken cancellationToken);
    Task<AccessKeyDto> CreateAsync(AccessKey key, CancellationToken cancellationToken);
    Task<AccessKeyDto?> RevokeAsync(Guid id, DateTimeOffset now, CancellationToken cancellationToken);
    Task<AccessKeyDto?> RotateAsync(Guid id, GeneratedAccessKey secret, Guid replacementId,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<AccessKeyDto?> SetGrantsAsync(Guid id, IReadOnlyList<Guid> ids,
        DateTimeOffset now, CancellationToken cancellationToken);
    Task<AccessKeyDto?> SetAccessAsync(Guid id, AccessKeyAccessUpdate command,
        DateTimeOffset now, CancellationToken cancellationToken);
}
