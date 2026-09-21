using ContextDepot.Application.Depots.Dtos;

namespace ContextDepot.Application.Depots.Contracts;

public interface IDepotRepository
{
    Task<IReadOnlyList<DepotSummary>> ListAsync(CancellationToken cancellationToken);
}
