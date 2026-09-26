using ContextDepot.Application.Embeddings.Dtos;
using ContextDepot.Domain.Inferences.Enums;
using ContextDepot.Infrastructure.CurrentDepot;
using ContextDepot.Infrastructure.RuntimeConfiguration;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ContextDepot.Infrastructure.HealthChecks;

public sealed class VectorCoverageHealthCheck(
    IServiceScopeFactory scopeFactory,
    InferenceRuntimeSnapshotAccessor inferenceSnapshotAccessor,
    TimeProvider timeProvider) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var inferenceSnapshot = inferenceSnapshotAccessor.Current;
        if (inferenceSnapshot.Embedding is null)
        {
            return HealthCheckResult.Degraded(
                inferenceSnapshot.State == InferenceRuntimeState.Unconfigured
                    ? "Embedding is not configured; lexical retrieval remains available."
                    : "Embedding is unavailable; lexical retrieval remains available.",
                data: new Dictionary<string, object>
                {
                    ["retrieval_degraded"] = true,
                    ["inference_state"] = inferenceSnapshot.State.ToString(),
                    ["inference_reason"] = inferenceSnapshot.DegradedReason ?? string.Empty
                });
        }

        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            scope.ServiceProvider.GetRequiredService<CurrentDepotAccessContext>().AllowInternalAccess();
            var db = scope.ServiceProvider.GetRequiredService<ContextDepotDbContext>();
            var snapshotProvider = scope.ServiceProvider.GetRequiredService<VectorCoverageSnapshotProvider>();
            var depotIds = await db.Depots
                .AsNoTracking()
                .OrderBy(depot => depot.Id)
                .Select(depot => depot.Id)
                .ToListAsync(cancellationToken);
            var snapshotsByDepot = await snapshotProvider.GetAsync(
                depotIds,
                timeProvider.GetUtcNow(),
                cancellationToken);
            var snapshots = depotIds
                .Select(depotId => snapshotsByDepot[depotId])
                .ToArray();

            var contextTotal = snapshots.Sum(snapshot => snapshot.ContextTotal);
            var contextIndexed = snapshots.Sum(snapshot => snapshot.ContextIndexed);
            var documentChunkTotal = snapshots.Sum(snapshot => snapshot.DocumentChunkTotal);
            var documentChunkIndexed = snapshots.Sum(snapshot => snapshot.DocumentChunkIndexed);
            var isComplete = snapshots.All(snapshot => snapshot.IsComplete);
            var contextCoverage = CalculateCoverage(contextTotal, contextIndexed);
            var documentCoverage = CalculateCoverage(documentChunkTotal, documentChunkIndexed);
            var data = new Dictionary<string, object>
            {
                ["depot_count"] = depotIds.Count,
                ["context_total"] = contextTotal,
                ["context_indexed"] = contextIndexed,
                ["context_coverage"] = contextCoverage,
                ["document_chunk_total"] = documentChunkTotal,
                ["document_chunk_indexed"] = documentChunkIndexed,
                ["document_coverage"] = documentCoverage,
                ["retrieval_degraded"] = !isComplete
            };
            return isComplete
                ? HealthCheckResult.Healthy("Vector coverage is complete for all depots.", data)
                : HealthCheckResult.Degraded(
                    "Vector coverage is still being repaired for one or more depots.",
                    data: data);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Degraded(
                "Vector coverage is temporarily unavailable.",
                exception,
                new Dictionary<string, object> { ["retrieval_degraded"] = true });
        }
    }

    private static double CalculateCoverage(int total, int indexed) =>
        total == 0 ? 1 : Math.Clamp(indexed / (double)total, 0, 1);
}
