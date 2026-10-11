using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Settings.Contracts;

public interface IAccessKeyAppService
{
    Task<AccessKeyListDto> ListAsync(CancellationToken cancellationToken);
    Task<IssuedAccessKeyDto> CreateAsync(CreateAccessKeyRequest request, CancellationToken cancellationToken);
    Task<AccessKeyDto> RevokeAsync(Guid id, CancellationToken cancellationToken);
    Task<IssuedAccessKeyDto> RotateAsync(Guid id, CancellationToken cancellationToken);
    Task<AccessKeyDto> SetGrantsAsync(Guid id, UpdateAccessKeyGrantsRequest request, CancellationToken cancellationToken);
}
