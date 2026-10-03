using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Settings.Contracts;

public interface IInferenceSettingsRepository
{
    Task<IReadOnlyList<InferenceRouteDto>> GetAsync(CancellationToken cancellationToken);
    Task<InferenceRouteDto> SaveAsync(string capability, SaveInferenceRouteRequest request, CancellationToken cancellationToken);
    Task<InferenceProviderSettingsDto> GetProvidersAsync(CancellationToken cancellationToken);
    Task<InferenceProviderSettingsDto> SaveProviderAsync(SaveInferenceProviderRequest request, CancellationToken cancellationToken);
}
