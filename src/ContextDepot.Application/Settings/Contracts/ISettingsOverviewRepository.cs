using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Settings.Contracts;

public interface ISettingsOverviewRepository
{
    Task<SettingsOverviewDto> GetAsync(Guid depotId, string depotName, CancellationToken cancellationToken);
}
