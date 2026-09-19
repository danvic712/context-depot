using ContextDepot.Application.IndexRepair.Dtos;

namespace ContextDepot.Application.IndexRepair.Contracts;

public interface IIndexRepairAppService
{
    Task<IndexRepairCycleResult> RepairAsync(
        IndexRepairRequest request,
        CancellationToken cancellationToken);
}
