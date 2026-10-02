using ContextDepot.Application.Settings.Contracts;
using ContextDepot.Application.Settings.Dtos;
using ContextDepot.Application.Shared.Runtime.Contracts;

namespace ContextDepot.Application.Settings;

public sealed class SettingsOverviewAppService(ISettingsOverviewRepository repository, ICurrentDepotContext depot,
    IWorkspaceAccessContext access)
{
    public Task<SettingsOverviewDto> GetAsync(CancellationToken cancellationToken)
    {
        SettingsAccess.RequireManagement(depot, access);
        return repository.GetAsync(depot.DepotId, depot.DisplayName, cancellationToken);
    }
}
