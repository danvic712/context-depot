using ContextDepot.Application.Settings.Dtos;

namespace ContextDepot.Application.Settings.Contracts;

public interface ISettingsOverviewRepository
{
    Task<SettingsOverviewDto> GetAsync(CancellationToken cancellationToken);
}
