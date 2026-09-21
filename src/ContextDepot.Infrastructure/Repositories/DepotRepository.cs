using ContextDepot.Application.Depots.Contracts;
using ContextDepot.Application.Depots.Dtos;
using Microsoft.EntityFrameworkCore;

namespace ContextDepot.Infrastructure.Repositories;

public sealed class DepotRepository(ContextDepotDbContext db) : IDepotRepository
{
    public async Task<IReadOnlyList<DepotSummary>> ListAsync(CancellationToken cancellationToken) =>
        await db.Depots
            .AsNoTracking()
            .OrderBy(depot => depot.Id)
            .Select(depot => new DepotSummary(depot.Id, depot.DisplayName))
            .ToListAsync(cancellationToken);
}
