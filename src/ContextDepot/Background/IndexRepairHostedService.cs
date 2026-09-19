using ContextDepot.Application.Embeddings;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace ContextDepot.Background;

public sealed class IndexRepairHostedService(
    IServiceScopeFactory scopeFactory,
    IOptions<EmbeddingOptions> options,
    ILogger<IndexRepairHostedService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Guid? contextAfterId = null;
        Guid? documentChunkAfterId = null;
        var repairOptions = options.Value.Repair;

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(
                    TimeSpan.FromSeconds(repairOptions.PollIntervalSeconds),
                    stoppingToken);

                await using var scope = scopeFactory.CreateAsyncScope();
                var repairService = scope.ServiceProvider.GetRequiredService<IIndexRepairAppService>();
                var result = await repairService.RepairAsync(
                    new IndexRepairRequest(
                        repairOptions.BatchSize,
                        repairOptions.MaxBatchesPerCycle,
                        contextAfterId,
                        documentChunkAfterId),
                    stoppingToken);
                contextAfterId = result.NextContextAfterId;
                documentChunkAfterId = result.NextDocumentChunkAfterId;

                logger.LogInformation(
                    "Index repair cycle completed. {DocumentsReconciled} documents reconciled, {ContextVectorsCreatedOrUpdated} context vectors updated, {DocumentVectorsCreatedOrUpdated} document vectors updated, context wrapped={ContextScanWrapped}, document wrapped={DocumentScanWrapped}, retrieval degraded={RetrievalDegraded}.",
                    result.DocumentsReconciled,
                    result.ContextVectorsCreatedOrUpdated,
                    result.DocumentVectorsCreatedOrUpdated,
                    result.ContextScanWrapped,
                    result.DocumentScanWrapped,
                    result.RetrievalDegraded);
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
}
