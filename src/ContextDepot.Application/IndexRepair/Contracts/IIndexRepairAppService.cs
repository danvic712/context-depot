using ContextDepot.Application.IndexRepair.Dtos;

namespace ContextDepot.Application.IndexRepair.Contracts;

public interface IIndexRepairAppService
{
    Task<IndexRepairCycleResult> RepairAsync(
        Guid depotId,
        IndexRepairRequest request,
        CancellationToken cancellationToken);
}
