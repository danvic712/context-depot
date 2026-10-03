using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Settings.Contracts;

public interface IAiSettingsRepository
{
    Task<IReadOnlyList<AiRouteDto>> GetAsync(CancellationToken cancellationToken);
    Task<AiRouteDto> SaveAsync(string capability, SaveAiRouteRequest request, CancellationToken cancellationToken);
    Task<AiProviderSettingsDto> GetProvidersAsync(CancellationToken cancellationToken);
    Task<AiProviderSettingsDto> SaveProviderAsync(SaveAiProviderRequest request, CancellationToken cancellationToken);
}
