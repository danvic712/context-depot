using ContextDepot.Application.Bootstrap.Dtos;

namespace ContextDepot.Application.Bootstrap.Contracts;

public interface IContextBootstrapAppService
{
    Task<BootstrapResult> BootstrapAsync(BootstrapRequest request, CancellationToken cancellationToken);
}
