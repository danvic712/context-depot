using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Settings.Contracts;

public interface IAiSettingsRepository
{
    Task<IReadOnlyList<AiRouteDto>> GetAsync(CancellationToken cancellationToken);
    Task<AiRouteDto> SaveAsync(string capability, SaveAiRouteRequest request, CancellationToken cancellationToken);
}
