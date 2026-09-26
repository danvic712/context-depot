using ContextDepot.Application.Depots.Contracts;
using ContextDepot.Application.IndexRepair;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using ContextDepot.Infrastructure.CurrentDepot;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Jobs;

public sealed class IndexRepairCycleRunner(
    IServiceScopeFactory scopeFactory,
    InferenceRuntimeSnapshotAccessor inferenceSnapshotAccessor,
    ILogger<IndexRepairCycleRunner> logger)
{
    private readonly Dictionary<Guid, DepotRepairCursor> cursors = new();
    private string? lastProfileFingerprint;

    public async Task RunAsync(IndexRepairOptions repairOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repairOptions);

        var embedding = inferenceSnapshotAccessor.Current.Embedding;
        if (embedding is not null &&
            !string.Equals(lastProfileFingerprint, embedding.ProfileFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            cursors.Clear();
            lastProfileFingerprint = embedding.ProfileFingerprint;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<CurrentDepotAccessContext>().AllowInternalAccess();
        var depotRepository = scope.ServiceProvider.GetRequiredService<IDepotRepository>();
        var repairService = scope.ServiceProvider.GetRequiredService<IIndexRepairAppService>();
        var depots = await depotRepository.ListAsync(cancellationToken);
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
                        cursor?.DocumentChunkAfterId,
                        RepairVectors: embedding is not null),
                    cancellationToken);
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
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
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

    private sealed record DepotRepairCursor(
        Guid? ContextAfterId,
        Guid? DocumentChunkAfterId);
}
