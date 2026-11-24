using ContextDepot.Application.IndexRepair;
using ContextDepot.Application.IndexRepair.Contracts;
using ContextDepot.Application.IndexRepair.Dtos;
using ContextDepot.Infrastructure.Access;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using ContextDepot.Infrastructure.VectorStore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ContextDepot.Infrastructure.Jobs;

public sealed class IndexRepairCycleRunner(
    IServiceScopeFactory scopeFactory,
    InferenceRuntimeSnapshotAccessor inferenceSnapshotAccessor,
    ILogger<IndexRepairCycleRunner> logger)
{
    private Guid? contextAfterId;
    private Guid? documentChunkAfterId;
    private string? lastProfileFingerprint;

    public async Task RunAsync(IndexRepairOptions repairOptions, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(repairOptions);

        var embedding = inferenceSnapshotAccessor.Current.Embedding;
        if (embedding is not null &&
            !string.Equals(lastProfileFingerprint, embedding.ProfileFingerprint, StringComparison.OrdinalIgnoreCase))
        {
            contextAfterId = null;
            documentChunkAfterId = null;
            lastProfileFingerprint = embedding.ProfileFingerprint;
        }

        await using var scope = scopeFactory.CreateAsyncScope();
        scope.ServiceProvider.GetRequiredService<WorkspaceAccessContext>().AllowInternalAccess();
        var repairService = scope.ServiceProvider.GetRequiredService<IIndexRepairAppService>();
        try
        {
            await scope.ServiceProvider.GetRequiredService<PostgreSqlVectorStore>()
                .RemoveObsoleteVectorsAsync(repairOptions.BatchSize,
                    scope.ServiceProvider.GetRequiredService<TimeProvider>().GetUtcNow(), cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogWarning(exception, "Obsolete vector cleanup failed. The next cycle will retry.");
        }

        try
        {
            var result = await repairService.RepairAsync(new IndexRepairRequest(repairOptions.BatchSize,
                repairOptions.MaxBatchesPerCycle, contextAfterId, documentChunkAfterId, RepairVectors: embedding is not null), cancellationToken);
            contextAfterId = result.NextContextAfterId;
            documentChunkAfterId = result.NextDocumentChunkAfterId;
            logger.LogInformation(
                "Index repair completed. {DocumentsReconciled} documents reconciled, {ContextVectorsUpdated} context vectors updated, {DocumentVectorsUpdated} document vectors updated, context wrapped={ContextScanWrapped}, document wrapped={DocumentScanWrapped}, retrieval degraded={RetrievalDegraded}.",
                result.DocumentsReconciled, result.ContextVectorsCreatedOrUpdated, result.DocumentVectorsCreatedOrUpdated,
                result.ContextScanWrapped, result.DocumentScanWrapped, result.RetrievalDegraded);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            logger.LogError(exception, "Index repair failed. The next cycle will retry with the same cursors.");
        }
    }
}
