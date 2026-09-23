using ContextDepot.Application.IndexRepair;
using ContextDepot.Application.Depots.Contracts;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.Configuration;
using Microsoft.Extensions.Options;

namespace ContextDepot.BackgroundServices;

public sealed class IndexRepairHostedService(
    IServiceScopeFactory scopeFactory,
    IOptionsMonitor<IndexRepairOptions> options,
    InferenceRuntimeSnapshotAccessor inferenceSnapshotAccessor,
    ILogger<IndexRepairHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var cursors = new Dictionary<Guid, DepotRepairCursor>();
        while (!stoppingToken.IsCancellationRequested)
        {
            var repairOptions = options.CurrentValue;
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(repairOptions.PollIntervalSeconds),
                    stoppingToken);

                if (inferenceSnapshotAccessor.Current.Embedding is null)
                {
                    continue;
                }

                await using var scope = scopeFactory.CreateAsyncScope();
                scope.ServiceProvider.GetRequiredService<CurrentDepotAccessContext>().AllowInternalAccess();
                var depotRepository = scope.ServiceProvider.GetRequiredService<IDepotRepository>();
                var repairService = scope.ServiceProvider.GetRequiredService<IIndexRepairAppService>();
                var depots = await depotRepository.ListAsync(stoppingToken);
                var activeDepotIds = depots.Select(depot => depot.Id).ToHashSet();

                foreach (var depot in depots)
                {
                    cursors.TryGetValue(depot.Id, out var cursor);
                    try
                    {
                        var result = await repairService.RepairAsync(
                            depot.Id,
                            new IndexRepairRequest(
                                repairOptions.BatchSize,
                                repairOptions.MaxBatchesPerCycle,
                                cursor?.ContextAfterId,
                                cursor?.DocumentChunkAfterId),
                            stoppingToken);
                        cursors[depot.Id] = new DepotRepairCursor(
                            result.NextContextAfterId,
                            result.NextDocumentChunkAfterId);

                        logger.LogInformation(
                            "Index repair cycle completed for depot {DepotId} ({DepotDisplayName}). {DocumentsReconciled} documents reconciled, {ContextVectorsCreatedOrUpdated} context vectors updated, {DocumentVectorsCreatedOrUpdated} document vectors updated, context wrapped={ContextScanWrapped}, document wrapped={DocumentScanWrapped}, retrieval degraded={RetrievalDegraded}.",
                            depot.Id,
                            depot.DisplayName,
                            result.DocumentsReconciled,
                            result.ContextVectorsCreatedOrUpdated,
                            result.DocumentVectorsCreatedOrUpdated,
                            result.ContextScanWrapped,
                            result.DocumentScanWrapped,
                            result.RetrievalDegraded);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
                    {
                        throw;
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(
                            exception,
                            "Index repair cycle failed for depot {DepotId}; its current cursor will be retried.",
                            depot.Id);
                    }
                }

                foreach (var depotId in cursors.Keys.Where(depotId => !activeDepotIds.Contains(depotId)).ToArray())
                {
                    cursors.Remove(depotId);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Index repair cycle failed; the current cursor will be retried.");
            }
        }
    }

    private sealed record DepotRepairCursor(
        Guid? ContextAfterId,
        Guid? DocumentChunkAfterId);
}
